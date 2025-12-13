using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;
using webapp.Models;

namespace webapp.Services;

public interface ISearchResultsRepository
{
    Task<SearchSessionResults?> GetLatestResultsAsync(CancellationToken ct = default);
    Task<Photo?> GetPhotoInfoByMd5Async(string md5, CancellationToken ct = default);
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
        await using (var cmd = new NpgsqlCommand("SELECT id FROM search_session ORDER BY created_at DESC LIMIT 1",
                         conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                sessionId = reader.GetGuid(0);
            }
        }

        if (sessionId is null) return null;

        var results = new List<SearchResultRow>();
        await using (var cmd = new NpgsqlCommand(@"
            SELECT r.rank, r.score, r.path_md5
            FROM search_session_result r
            LEFT JOIN photo p ON p.md5_hash = r.path_md5
            WHERE r.session_id = @sid
            ORDER BY r.rank ASC", conn))
        {
            cmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId.Value);
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var row = new SearchResultRow
                {
                    Rank = rdr.GetInt32(0),
                    Score = rdr.GetFloat(1),
                    Md5 = rdr.IsDBNull(2) ? null : rdr.GetString(2)
                };
                results.Add(row);
            }
        }

        return new SearchSessionResults(sessionId.Value, results);
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
}

public sealed record SearchSessionResults(Guid SessionId, IReadOnlyList<SearchResultRow> Results);

public sealed record SearchResultRow
{
    public int Rank { get; init; }
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