using Microsoft.Extensions.Options;
using Npgsql;
using webapp.Models;
using NpgsqlTypes;

namespace webapp.Services;

public interface IContentValidationRepository
{
    Task<IReadOnlyList<ValidationRow>> GetByJobAsync(Guid jobId, CancellationToken ct = default);
    Task<IReadOnlyList<ValidationRow>> GetLatestAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ValidationDetailRow>> GetLatestDetailsByFolderAsync(string folder, CancellationToken ct = default);
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
            "select folder, test_kind, status, (details->>'total')::int as total from content_validation_result where job_id = @job order by folder", conn);
        cmd.Parameters.AddWithValue("@job", jobId);
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            var total = rdr.IsDBNull(3) ? (int?)null : rdr.GetInt32(3);
            list.Add(new ValidationRow(rdr.GetString(0), rdr.GetString(1), rdr.GetString(2), total));
        }
        return list;
    }

    public async Task<IReadOnlyList<ValidationRow>> GetLatestAsync(CancellationToken ct = default)
    {
        var list = new List<ValidationRow>();
        await using var conn = Create();
        await conn.OpenAsync(ct);
        // pick the latest row per (folder, test_kind) by finished_at/started_at
        const string sql = @"
            SELECT DISTINCT ON (folder, test_kind)
                   folder, test_kind, status, (details->>'total')::int as total
            FROM content_validation_result
            ORDER BY folder, test_kind, COALESCE(finished_at, started_at) DESC;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            var total = rdr.IsDBNull(3) ? (int?)null : rdr.GetInt32(3);
            list.Add(new ValidationRow(rdr.GetString(0), rdr.GetString(1), rdr.GetString(2), total));
        }
        return list;
    }

    public async Task<IReadOnlyList<ValidationDetailRow>> GetLatestDetailsByFolderAsync(string folder, CancellationToken ct = default)
    {
        var list = new List<ValidationDetailRow>();
        await using var conn = Create();
        await conn.OpenAsync(ct);
        const string sql = @"
            SELECT DISTINCT ON (folder, test_kind)
                   test_kind, status, details::text
            FROM content_validation_result
            WHERE folder = @folder
            ORDER BY folder, test_kind, COALESCE(finished_at, started_at) DESC;";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@folder", folder);
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            var testKind = rdr.GetString(0);
            var status = rdr.GetString(1);
            var details = rdr.IsDBNull(2) ? null : rdr.GetString(2);
            list.Add(new ValidationDetailRow(testKind, status, details));
        }
        return list;
    }
}

public sealed record ValidationRow(string Folder, string TestKind, string Status, int? TotalFailures);
public sealed record ValidationDetailRow(string TestKind, string Status, string? DetailsJson);
