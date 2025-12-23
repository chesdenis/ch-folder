using Microsoft.Extensions.Options;
using Npgsql;
using webapp.Models;

namespace webapp.Services;

public interface IContentValidationRepository
{
    Task<IReadOnlyList<ValidationRow>> GetByJobAsync(Guid jobId, CancellationToken ct = default);
    Task<IReadOnlyList<ValidationRow>> GetLatestByTestKindAsync(string testKind, CancellationToken ct = default);
}

public sealed class ContentValidationRepository(IOptions<ConnectionStringOptions> connectionStrings)
    : IContentValidationRepository
{
    private readonly ConnectionStringOptions _cs = connectionStrings.Value;

    private NpgsqlConnection Create() =>
        new(_cs.PgPhMetaDb ?? throw new InvalidOperationException("ConnectionStrings:PgPhMetaDb is not configured"));

    public async Task<IReadOnlyList<ValidationRow>> GetByJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var list = new List<ValidationRow>();
        await using var conn = Create();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "select folder, test_kind, status from content_validation_result where job_id = @job order by folder", conn);
        cmd.Parameters.AddWithValue("@job", jobId);
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            list.Add(new ValidationRow(rdr.GetString(0), rdr.GetString(1), rdr.GetString(2)));
        }
        return list;
    }

    public async Task<IReadOnlyList<ValidationRow>> GetLatestByTestKindAsync(string testKind, CancellationToken ct = default)
    {
        var list = new List<ValidationRow>();
        await using var conn = Create();
        await conn.OpenAsync(ct);
        // pick the latest row per (folder, test_kind) by finished_at/started_at
        const string sql = @"
            SELECT DISTINCT ON (folder, test_kind)
                   folder, test_kind, status
            FROM content_validation_result
            WHERE test_kind = @kind
            ORDER BY folder, test_kind, COALESCE(finished_at, started_at) DESC;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@kind", testKind);
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            list.Add(new ValidationRow(rdr.GetString(0), rdr.GetString(1), rdr.GetString(2)));
        }
        return list;
    }
}

public sealed record ValidationRow(string Folder, string TestKind, string Status);
