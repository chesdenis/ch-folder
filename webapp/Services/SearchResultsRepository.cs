using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;
using webapp.Models;

namespace webapp.Services;

public interface ISearchResultsRepository
{
    Task<SearchSessionResults?> GetLatestResultsAsync(CancellationToken ct = default);
    Task<SearchSessionResults?> GetResultsBySessionIdAsync(Guid sessionId, float? minScore = null, CancellationToken ct = default);
    Task<Photo?> GetPhotoInfoByMd5Async(string md5, CancellationToken ct = default);
    Task<int> GetPhotosCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRecentPhotoMd5Async(int offset, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDistinctTagsForSessionAsync(Guid sessionId, CancellationToken ct = default);
}

public sealed class SearchResultsRepository(IOptions<ConnectionStringOptions> connectionStringsOptions)
    : ISearchResultsRepository
{
    private readonly ConnectionStringOptions _connectionStrings = connectionStringsOptions.Value;

    private NpgsqlConnection CreateConnection() =>
        new(_connectionStrings.PgPhMetaDb ?? throw new InvalidOperationException());

    public async Task<SearchSessionResults?> GetLatestResultsAsync(CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // Get the latest session id
        Guid? sessionId = null;
        string? queryText = null;
        await using (var cmd = new NpgsqlCommand("SELECT id, query_text FROM search_session ORDER BY created_at DESC LIMIT 1",
                         conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                sessionId = reader.GetGuid(0);
                queryText = reader.GetString(1);
            }
        }

        if (sessionId is null) return null;

        var results = new List<SearchResultRow>();
        await using (var cmd = new NpgsqlCommand(@"
            SELECT r.score, r.path_md5
            FROM search_session_result r
            LEFT JOIN photo p ON p.md5_hash = r.path_md5
            WHERE r.session_id = @sid
            ORDER BY r.score DESC, r.path_md5 ASC", conn))
        {
            cmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId.Value);
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var row = new SearchResultRow
                {
                    Score = rdr.GetFloat(0),
                    Md5 = rdr.IsDBNull(1) ? null : rdr.GetString(1)
                };
                results.Add(row);
            }
        }

        return new SearchSessionResults(sessionId.Value, queryText ?? string.Empty, results);
    }

    public async Task<SearchSessionResults?> GetResultsBySessionIdAsync(Guid sessionId, float? minScore = null, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // Verify session exists and fetch its query_text
        string? queryText = null;
        await using (var checkCmd = new NpgsqlCommand("SELECT query_text FROM search_session WHERE id = @sid LIMIT 1", conn))
        {
            checkCmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId);
            var scalar = await checkCmd.ExecuteScalarAsync(ct);
            queryText = scalar as string;
        }

        if (queryText is null) return null;

        var results = new List<SearchResultRow>();
        var sql = @"SELECT r.score, r.path_md5
            FROM search_session_result r
            LEFT JOIN photo p ON p.md5_hash = r.path_md5
            WHERE r.session_id = @sid";
        if (minScore.HasValue)
        {
            sql += " AND r.score >= @min";
        }
        sql += " ORDER BY r.score DESC, r.path_md5 ASC";
        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId);
            if (minScore.HasValue)
            {
                cmd.Parameters.AddWithValue("@min", NpgsqlTypes.NpgsqlDbType.Real, minScore.Value);
            }
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var row = new SearchResultRow
                {
                    Score = rdr.GetFloat(0),
                    Md5 = rdr.IsDBNull(1) ? null : rdr.GetString(1)
                };
                results.Add(row);
            }
        }

        return new SearchSessionResults(sessionId, queryText, results);
    }

    public async Task<Photo?> GetPhotoInfoByMd5Async(string md5, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"SELECT md5_hash,
            extension,
            size_bytes,
            tags,
            short_details,
            created_at,
            updated_at FROM photo WHERE md5_hash = @md5 LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@md5", NpgsqlTypes.NpgsqlDbType.Text, md5);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var photo = new Photo
        {
            Md5Hash = reader.GetString(0),
            Extension = reader.GetString(1),
            SizeBytes = reader.GetInt64(2),
            Tags = reader.IsDBNull(3) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(3),
            ShortDetails = reader.GetString(4),
            CreatedAt = reader.GetDateTime(5), // UTC DateTime for timestamptz
            UpdatedAt = reader.GetDateTime(6)
        };

        return photo;
    }

    public async Task<int> GetPhotosCountAsync(CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM photo", conn);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task<IReadOnlyList<string>> GetRecentPhotoMd5Async(int offset, int limit, CancellationToken ct = default)
    {
        if (limit <= 0) return Array.Empty<string>();
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var list = new List<string>(limit);
        await using var cmd = new NpgsqlCommand(@"SELECT md5_hash FROM photo ORDER BY created_at DESC LIMIT @lim OFFSET @off", conn);
        cmd.Parameters.AddWithValue("@lim", NpgsqlTypes.NpgsqlDbType.Integer, limit);
        cmd.Parameters.AddWithValue("@off", NpgsqlTypes.NpgsqlDbType.Integer, Math.Max(0, offset));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(reader.GetString(0));
        }
        return list;
    }

    public async Task<IReadOnlyList<string>> GetDistinctTagsForSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var tags = new List<string>();
        // Flatten tags array from photos that appear in the session results and return distinct sorted list
        await using var cmd = new NpgsqlCommand(@"
            SELECT DISTINCT t
            FROM search_session_result r
            JOIN photo p ON p.md5_hash = r.path_md5
            CROSS JOIN LATERAL unnest(p.tags) AS t
            WHERE r.session_id = @sid
            ORDER BY t ASC", conn);
        cmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var val = reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(val))
                tags.Add(val);
        }
        return tags;
    }
}

public sealed record SearchSessionResults(Guid SessionId, string QueryText, IReadOnlyList<SearchResultRow> Results);

public sealed record SearchResultRow
{
    public float Score { get; init; }
    public string? Md5 { get; init; }
}

public sealed record Photo
{
    public string Md5Hash { get; init; }
    public string Extension { get; set; }
    public long SizeBytes { get; set; }
    public string[] Tags { get; set; }
    public string ShortDetails { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}