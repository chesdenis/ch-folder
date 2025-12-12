using System.Data;
using Npgsql;

namespace webapp.Services;

public interface ISearchResultsRepository
{
    Task<SearchSessionResults?> GetLatestResultsAsync(CancellationToken ct = default);
    Task<Photo?> GetPhotoInfoByMd5Async(string md5, CancellationToken ct = default);
}

public sealed class SearchResultsRepository : ISearchResultsRepository
{
    private readonly ILogger<SearchResultsRepository> _logger;

    public SearchResultsRepository(ILogger<SearchResultsRepository> logger)
    {
        _logger = logger;
    }

    private static string BuildPgConnectionString()
    {
        // Match ImageSearcher.cs env-based configuration
        string host = Environment.GetEnvironmentVariable("PG_HOST");
        string port = Environment.GetEnvironmentVariable("PG_PORT");
        string database = Environment.GetEnvironmentVariable("PG_DATABASE");
        string username = Environment.GetEnvironmentVariable("PG_USERNAME");
        string password = Environment.GetEnvironmentVariable("PG_PASSWORD");

        var parts = new[]
        {
            $"Host={host}",
            $"Port={port}",
            $"Database={database}",
            $"Username={username}",
            $"Password={password}",
            "Ssl Mode=Disable",
            "Trust Server Certificate=true",
            "Include Error Detail=true"
        };
        return string.Join(";", parts);
    }

    private static NpgsqlConnection CreateConnection() => new(BuildPgConnectionString());

    public async Task<SearchSessionResults?> GetLatestResultsAsync(CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // Get the latest session id
        Guid? sessionId = null;
        await using (var cmd = new NpgsqlCommand("SELECT id FROM search_session ORDER BY created_at DESC LIMIT 1", conn))
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
            SELECT r.rank, r.score, r.path_md5, p.file_path
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
                    Md5 = rdr.IsDBNull(2) ? null : rdr.GetString(2),
                    FilePath = rdr.IsDBNull(3) ? null : rdr.GetString(3)
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
            file_name,
            dir_name,
            extension,
            dir_path,
            file_path,
            size_bytes,
            tags,
            short_details,
            color_hash,
            average_hash,
            created_at,
            updated_at FROM photo WHERE md5_hash = @md5 LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@md5", NpgsqlTypes.NpgsqlDbType.Text, md5);
        
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;
        
        var photo = new Photo
        {
            Md5Hash     = reader.GetString(0),
            FileName    = reader.GetString(1),
            DirName     = reader.GetString(2),
            Extension   = reader.GetString(3),
            DirPath     = reader.GetString(4),
            FilePath    = reader.GetString(5),
            SizeBytes   = reader.GetInt64(6),
            Tags        = reader.IsDBNull(7) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(7),
            ShortDetails= reader.GetString(8),
            ColorHash   = reader.GetString(9),
            AverageHash = reader.GetString(10),
            CreatedAt   = reader.GetDateTime(11), // UTC DateTime for timestamptz
            UpdatedAt   = reader.GetDateTime(12)
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
    public string? FilePath { get; init; }
}

public sealed record Photo
{
    public string Md5Hash { get; init; }  
    public string FileName { get; set; }  
    public string DirName { get; set; } 
    public string Extension { get; set; }  
    public string DirPath { get; set; }  
    public string FilePath { get; set; }  
    public long SizeBytes { get; set; }
    public string[] Tags { get; set; }
    public string ShortDetails { get; set; }  
    public string ColorHash { get; set; }  
    public string AverageHash { get; set; }  
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
