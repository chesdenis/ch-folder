using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;
using webapp.Models;

namespace webapp.Services;

public enum ResultsOrderBy
{
    ScoreDesc,
    CommerceDesc
}

public interface ISearchResultsRepository
{
    Task<SearchSessionResults?> GetLatestResultsAsync(ResultsOrderBy orderBy = ResultsOrderBy.ScoreDesc, CancellationToken ct = default);
    Task<SearchSessionResults?> GetResultsBySessionIdAsync(Guid sessionId, float? minScore = null, ResultsOrderBy orderBy = ResultsOrderBy.ScoreDesc, CancellationToken ct = default);
    Task<Photo?> GetPhotoInfoByMd5Async(string md5, CancellationToken ct = default);
    Task<IReadOnlyList<Photo>> GetPhotosByGroupAsync(string groupName, CancellationToken ct = default);
    Task<int> GetPhotosCountAsync(string[]? tags = null, string[]? persons = null, int[]? commerceRatings = null, string[]? partitions = null, string[]? sections = null, string[]? extensions = null, bool groupByGroup = false, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRecentPhotoMd5Async(int offset, int limit, string[]? tags = null, string[]? persons = null, int[]? commerceRatings = null, string[]? partitions = null, string[]? sections = null, string[]? extensions = null, bool groupByGroup = false, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAllDistinctTagsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetTagsAsync(string? searchTerm, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAllDistinctPersonsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPersonsAsync(string? searchTerm, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAllDistinctFoldersAsync(CancellationToken ct = default);
    Task<IDictionary<string, List<string>>> GetPartitionSectionHierarchyAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAllDistinctExtensionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Photo>> GetPhotosByMd5sAsync(IReadOnlyList<string> md5s, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDistinctTagsForSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDistinctPersonsForSessionAsync(Guid sessionId, CancellationToken ct = default);
}

public sealed class SearchResultsRepository(IOptions<ConnectionStringOptions> connectionStringsOptions)
    : ISearchResultsRepository
{
    private readonly ConnectionStringOptions _connectionStrings = connectionStringsOptions.Value;

    private NpgsqlConnection CreateConnection() =>
        new(_connectionStrings.PgPhMetaDb ?? throw new InvalidOperationException());

    public async Task<SearchSessionResults?> GetLatestResultsAsync(ResultsOrderBy orderBy = ResultsOrderBy.ScoreDesc, CancellationToken ct = default)
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
        var orderSql = orderBy == ResultsOrderBy.CommerceDesc
            ? "ORDER BY COALESCE(p.commerce_rate, 0) DESC, r.score DESC"
            : "ORDER BY r.score DESC, COALESCE(p.commerce_rate, 0) DESC";
        await using (var cmd = new NpgsqlCommand($@"
            SELECT r.score, r.path_md5, p.commerce_rate, p.group_name
            FROM search_session_result r
            LEFT JOIN photo p ON p.md5_hash = r.path_md5
            WHERE r.session_id = @sid {orderSql}", conn))
        {
            cmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId.Value);
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var row = new SearchResultRow
                {
                    Score = rdr.GetFloat(0),
                    Md5 = rdr.IsDBNull(1) ? null : rdr.GetString(1),
                    CommerceRating = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2),
                    Group = rdr.IsDBNull(3) ? null : rdr.GetString(3)
                };
                results.Add(row);
            }
        }

        return new SearchSessionResults(sessionId.Value, queryText ?? string.Empty, results);
    }

    public async Task<SearchSessionResults?> GetResultsBySessionIdAsync(Guid sessionId, float? minScore = null, ResultsOrderBy orderBy = ResultsOrderBy.ScoreDesc, CancellationToken ct = default)
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
        var sql = @"SELECT r.score, r.path_md5, p.commerce_rate, p.group_name
            FROM search_session_result r
            LEFT JOIN photo p ON p.md5_hash = r.path_md5
            WHERE r.session_id = @sid";
        if (minScore.HasValue)
        {
            sql += " AND r.score >= @min";
        }
        sql += orderBy == ResultsOrderBy.CommerceDesc
            ? " ORDER BY COALESCE(p.commerce_rate, 0) DESC, r.score DESC"
            : " ORDER BY r.score DESC, COALESCE(p.commerce_rate, 0) DESC";
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
                    Md5 = rdr.IsDBNull(1) ? null : rdr.GetString(1),
                    CommerceRating = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2),
                    Group = rdr.IsDBNull(3) ? null : rdr.GetString(3)
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
            tags,
            short_details,
            created_at,
            updated_at,
            commerce_rate,
            group_name,
            ""partition"",
            ""section"" FROM photo WHERE md5_hash = @md5 LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@md5", NpgsqlTypes.NpgsqlDbType.Text, md5);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var photo = new Photo
        {
            Md5Hash = reader.GetString(0),
            Extension = reader.GetString(1),
            Tags = reader.IsDBNull(2) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(2),
            ShortDetails = reader.GetString(3),
            CreatedAt = reader.GetDateTime(4), // UTC DateTime for timestamptz
            UpdatedAt = reader.GetDateTime(5),
            CommerceRate = reader.GetInt32(6),
            GroupName = reader.GetString(7),
            Partition = reader.GetString(8),
            Section = reader.GetString(9)
        };

        return photo;
    }

    public async Task<IReadOnlyList<Photo>> GetPhotosByGroupAsync(string groupName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return Array.Empty<Photo>();
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var list = new List<Photo>();
        await using var cmd = new NpgsqlCommand(@"SELECT md5_hash,
            extension,
            tags,
            short_details,
            created_at,
            updated_at,
            commerce_rate,
            group_name,
            ""partition"",
            ""section"" FROM photo WHERE group_name = @group ORDER BY created_at ASC", conn);
        cmd.Parameters.AddWithValue("@group", NpgsqlTypes.NpgsqlDbType.Text, groupName);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Photo
            {
                Md5Hash = reader.GetString(0),
                Extension = reader.GetString(1),
                Tags = reader.IsDBNull(2) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(2),
                ShortDetails = reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                UpdatedAt = reader.GetDateTime(5),
                CommerceRate = reader.GetInt32(6),
                GroupName = reader.GetString(7),
                Partition = reader.GetString(8),
                Section = reader.GetString(9)
            });
        }
        return list;
    }

    private void AddCommonParameters(NpgsqlCommand cmd, string[]? tags, string[]? persons, int[]? commerceRatings, string[]? partitions, string[]? sections, string[]? extensions)
    {
        if (tags != null && tags.Length > 0)
        {
            cmd.Parameters.AddWithValue("@tags", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, tags);
        }
        if (persons != null && persons.Length > 0)
        {
            cmd.Parameters.AddWithValue("@persons", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, persons);
        }
        if (commerceRatings != null && commerceRatings.Length > 0)
        {
            cmd.Parameters.AddWithValue("@commerceRatings", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer, commerceRatings);
        }
        if (partitions != null && partitions.Length > 0)
        {
            cmd.Parameters.AddWithValue("@partitions", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, partitions);
        }
        if (sections != null && sections.Length > 0)
        {
            cmd.Parameters.AddWithValue("@sections", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, sections);
        }
        if (extensions != null && extensions.Length > 0)
        {
            cmd.Parameters.AddWithValue("@extensions", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, extensions!);
        }
    }

    public async Task<int> GetPhotosCountAsync(string[]? tags = null, string[]? persons = null, int[]? commerceRatings = null, string[]? partitions = null, string[]? sections = null, string[]? extensions = null, bool groupByGroup = false, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var usePartitions = partitions != null && partitions.Length > 0;
        var useSections = sections != null && sections.Length > 0;
        var useExtensions = extensions != null && extensions.Length > 0;
        var sql = groupByGroup 
            ? "SELECT COUNT(DISTINCT group_name) FROM photo p"
            : "SELECT COUNT(*) FROM photo p";

        sql += " WHERE 1=1";

        if (tags != null && tags.Length > 0)
        {
            sql += " AND p.tags @> @tags";
        }
        if (persons != null && persons.Length > 0)
        {
            sql += " AND p.persons @> @persons";
        }
        if (commerceRatings != null && commerceRatings.Length > 0)
        {
            sql += " AND p.commerce_rate = ANY(@commerceRatings)";
        }
        if (usePartitions)
        {
            sql += " AND p.\"partition\" = ANY(@partitions)";
        }
        if (useSections)
        {
            sql += " AND p.\"section\" = ANY(@sections)";
        }
        if (useExtensions)
        {
            sql += " AND p.extension = ANY(@extensions)";
        }

        await using var cmd = new NpgsqlCommand(sql, conn);
        AddCommonParameters(cmd, tags, persons, commerceRatings, partitions, sections, extensions);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task<IReadOnlyList<string>> GetRecentPhotoMd5Async(int offset, int limit, string[]? tags = null, string[]? persons = null, int[]? commerceRatings = null, string[]? partitions = null, string[]? sections = null, string[]? extensions = null, bool groupByGroup = false, CancellationToken ct = default)
    {
        if (limit <= 0) return Array.Empty<string>();
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var list = new List<string>(limit);
        
        var usePartitions = partitions != null && partitions.Length > 0;
        var useSections = sections != null && sections.Length > 0;
        var useExtensions = extensions != null && extensions.Length > 0;
        string sql;
        if (groupByGroup)
        {
            sql = @"
                SELECT md5_hash 
                FROM (
                    SELECT DISTINCT ON (p.group_name) p.md5_hash, p.created_at
                    FROM photo p";
            
            sql += " WHERE 1=1";
            
            if (tags != null && tags.Length > 0) sql += " AND p.tags @> @tags";
            if (persons != null && persons.Length > 0) sql += " AND p.persons @> @persons";
            if (commerceRatings != null && commerceRatings.Length > 0) sql += " AND p.commerce_rate = ANY(@commerceRatings)";
            if (usePartitions) sql += " AND p.\"partition\" = ANY(@partitions)";
            if (useSections) sql += " AND p.\"section\" = ANY(@sections)";
            if (useExtensions) sql += " AND p.extension = ANY(@extensions)";

            sql += @"
                    ORDER BY p.group_name, p.created_at DESC
                ) AS sub
                ORDER BY created_at DESC 
                LIMIT @lim OFFSET @off";
        }
        else
        {
            sql = "SELECT p.md5_hash FROM photo p";
            sql += " WHERE 1=1";
            if (tags != null && tags.Length > 0) sql += " AND p.tags @> @tags";
            if (persons != null && persons.Length > 0) sql += " AND p.persons @> @persons";
            if (commerceRatings != null && commerceRatings.Length > 0) sql += " AND p.commerce_rate = ANY(@commerceRatings)";
            if (usePartitions) sql += " AND p.\"partition\" = ANY(@partitions)";
            if (useSections) sql += " AND p.\"section\" = ANY(@sections)";
            if (useExtensions) sql += " AND p.extension = ANY(@extensions)";
            sql += " ORDER BY p.created_at DESC LIMIT @lim OFFSET @off";
        }

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@lim", NpgsqlTypes.NpgsqlDbType.Integer, limit);
        cmd.Parameters.AddWithValue("@off", NpgsqlTypes.NpgsqlDbType.Integer, Math.Max(0, offset));
        AddCommonParameters(cmd, tags, persons, commerceRatings, partitions, sections, extensions);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(reader.GetString(0));
        }
        return list;
    }

    public async Task<IReadOnlyList<string>> GetAllDistinctTagsAsync(CancellationToken ct = default)
    {
        return await GetTagsAsync(null, ct);
    }

    public async Task<IReadOnlyList<string>> GetTagsAsync(string? searchTerm, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var tags = new List<string>();
        var sql = @"
            SELECT t AS tag
            FROM photo
            CROSS JOIN LATERAL unnest(tags) AS t
            WHERE t IS NOT NULL AND length(trim(t)) > 0";
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            sql += " AND t ILIKE @term";
        }

        sql += @"
            GROUP BY t
            ORDER BY COUNT(*) DESC, t ASC LIMIT 100";

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            cmd.Parameters.AddWithValue("@term", $"%{searchTerm}%");
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tags.Add(reader.GetString(0));
        }

        return tags.OrderBy(t => t).ToList();
    }

    public async Task<IReadOnlyList<string>> GetAllDistinctPersonsAsync(CancellationToken ct = default)
    {
        return await GetPersonsAsync(null, ct);
    }

    public async Task<IReadOnlyList<string>> GetPersonsAsync(string? searchTerm, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var persons = new List<string>();
        var sql = @"
            SELECT p_name AS person
            FROM photo
            CROSS JOIN LATERAL unnest(persons) AS p_name
            WHERE p_name IS NOT NULL AND length(trim(p_name)) > 0";
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            sql += " AND p_name ILIKE @term";
        }

        sql += @"
            GROUP BY p_name
            ORDER BY COUNT(*) DESC, p_name ASC LIMIT 100";

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            cmd.Parameters.AddWithValue("@term", $"%{searchTerm}%");
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            persons.Add(reader.GetString(0));
        }

        return persons.OrderBy(p => p).ToList();
    }

    public async Task<IReadOnlyList<string>> GetAllDistinctFoldersAsync(CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var folders = new List<string>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT DISTINCT ""partition"" FROM photo WHERE ""partition"" <> ''
            UNION
            SELECT DISTINCT ""section"" FROM photo WHERE ""section"" <> ''", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var folder = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (!string.IsNullOrEmpty(folder))
            {
                folders.Add(folder);
            }
        }
        return folders.Distinct().OrderBy(x => x).ToList();
    }

    public async Task<IDictionary<string, List<string>>> GetPartitionSectionHierarchyAsync(CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var hierarchy = new Dictionary<string, List<string>>();
        await using var cmd = new NpgsqlCommand("SELECT DISTINCT \"partition\", \"section\" FROM photo WHERE \"partition\" <> '' ORDER BY \"partition\", \"section\"", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var p = reader.GetString(0);
            var s = reader.GetString(1);
            if (!hierarchy.ContainsKey(p)) hierarchy[p] = new List<string>();
            if (!string.IsNullOrEmpty(s)) hierarchy[p].Add(s);
        }
        return hierarchy;
    }

    public async Task<IReadOnlyList<string>> GetAllDistinctExtensionsAsync(CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var list = new List<string>();
        await using var cmd = new NpgsqlCommand("SELECT DISTINCT extension FROM photo ORDER BY extension", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(reader.GetString(0));
        }
        return list;
    }

    public async Task<IReadOnlyList<Photo>> GetPhotosByMd5sAsync(IReadOnlyList<string> md5s, CancellationToken ct = default)
    {
        if (md5s.Count == 0) return Array.Empty<Photo>();
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var list = new List<Photo>(md5s.Count);
        await using var cmd = new NpgsqlCommand(@"
            SELECT md5_hash,
            extension,
            tags,
            short_details,
            created_at,
            updated_at,
            commerce_rate,
            group_name,
            ""partition"",
            ""section"" FROM photo WHERE md5_hash = ANY(@md5s)", conn);
        cmd.Parameters.AddWithValue("@md5s", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, md5s.ToArray());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Photo
            {
                Md5Hash = reader.GetString(0),
                Extension = reader.GetString(1),
                Tags = reader.IsDBNull(2) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(2),
                ShortDetails = reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                UpdatedAt = reader.GetDateTime(5),
                CommerceRate = reader.GetInt32(6),
                GroupName = reader.GetString(7),
                Partition = reader.GetString(8),
                Section = reader.GetString(9)
            });
        }
        return list;
    }

    public async Task<IReadOnlyList<string>> GetDistinctTagsForSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var tags = new List<string>();
        // Return distinct tags for a session, prioritizing tags coming from higher-ranked (score) results
        // and higher frequency. We aggregate per tag and order by total score then frequency.
        await using var cmd = new NpgsqlCommand(@"
            SELECT t AS tag
            FROM search_session_result r
            JOIN photo p ON p.md5_hash = r.path_md5
            CROSS JOIN LATERAL unnest(p.tags) AS t
            WHERE r.session_id = @sid AND t IS NOT NULL AND length(trim(t)) > 0
            GROUP BY t
            ORDER BY SUM(r.score) DESC, COUNT(*) DESC, t ASC", conn);
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

    public async Task<IReadOnlyList<string>> GetDistinctPersonsForSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        var persons = new List<string>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT p_name AS person
            FROM search_session_result r
            JOIN photo p ON p.md5_hash = r.path_md5
            CROSS JOIN LATERAL unnest(p.persons) AS p_name
            WHERE r.session_id = @sid AND p_name IS NOT NULL AND length(trim(p_name)) > 0
            GROUP BY p_name
            ORDER BY SUM(r.score) DESC, COUNT(*) DESC, p_name ASC", conn);
        cmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var val = reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(val))
                persons.Add(val);
        }
        return persons;
    }
}

public sealed record SearchSessionResults(Guid SessionId, string QueryText, IReadOnlyList<SearchResultRow> Results);

public sealed record SearchResultRow
{
    public float Score { get; init; }
    public int CommerceRating { get; init; }
    public string? Md5 { get; init; }
    public string? Group { get; set; }
}

public sealed record Photo
{
    public string Md5Hash { get; init; }
    public string Extension { get; set; }
    public string[] Tags { get; set; }
    public string ShortDetails { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int CommerceRate { get; set; }
    public string GroupName { get; set; }
    public string Partition { get; init; }
    public string Section { get; init; }
    public string[] PublishPlatforms { get; set; } = Array.Empty<string>();
}