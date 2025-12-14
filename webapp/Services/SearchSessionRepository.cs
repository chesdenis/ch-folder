using Microsoft.Extensions.Options;
using Npgsql;
using webapp.Models;

namespace webapp.Services;

public interface ISearchSessionRepository
{
    Task<(IReadOnlyList<SearchSessionRow> Items, int Total)> GetRecentSessionsAsync(int offset, int limit, CancellationToken ct = default);
}

public sealed class SearchSessionRepository(IOptions<ConnectionStringOptions> connectionStringsOptions)
    : ISearchSessionRepository
{
    private readonly ConnectionStringOptions _connectionStrings = connectionStringsOptions.Value;

    private NpgsqlConnection CreateConnection() =>
        new(_connectionStrings.PgPhMetaDb ?? throw new InvalidOperationException());

    public async Task<(IReadOnlyList<SearchSessionRow> Items, int Total)> GetRecentSessionsAsync(int offset, int limit, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // total count
        int total;
        await using (var countCmd = new NpgsqlCommand("SELECT COUNT(1) FROM search_session", conn))
        {
            var scalar = await countCmd.ExecuteScalarAsync(ct);
            total = Convert.ToInt32(scalar);
        }

        var list = new List<SearchSessionRow>(Math.Max(0, limit));
        await using (var cmd = new NpgsqlCommand(@"SELECT id, created_at, query_text, embedding_model, result_count, score_threshold
            FROM search_session
            ORDER BY created_at DESC
            OFFSET @off LIMIT @lim", conn))
        {
            cmd.Parameters.AddWithValue("@off", NpgsqlTypes.NpgsqlDbType.Integer, Math.Max(0, offset));
            cmd.Parameters.AddWithValue("@lim", NpgsqlTypes.NpgsqlDbType.Integer, Math.Max(1, limit));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new SearchSessionRow
                {
                    Id = reader.GetGuid(0),
                    CreatedAt = reader.GetDateTime(1),
                    QueryText = reader.GetString(2),
                    EmbeddingModel = reader.GetString(3),
                    ResultCount = reader.GetInt32(4),
                    ScoreThreshold = reader.IsDBNull(5) ? (float?)null : reader.GetFloat(5)
                });
            }
        }

        return (list, total);
    }
}

public sealed class SearchSessionRow
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public float? ScoreThreshold { get; set; }
}
