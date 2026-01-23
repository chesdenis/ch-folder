using Microsoft.Extensions.Options;
using Npgsql;
using webapp.Models;

namespace webapp.Services;

public interface IBackupRepository
{
    Task<IReadOnlyList<BackupStatusRow>> GetAllAsync(CancellationToken ct = default);
    Task TruncateAsync(CancellationToken ct = default);
}

public sealed record BackupStatusRow(string Partition, string Folder, string ActivityKind, string Status, string? ErrorMessage);

public sealed class BackupRepository(IOptions<ConnectionStringOptions> connectionStrings) : IBackupRepository
{
    private readonly ConnectionStringOptions _cs = connectionStrings.Value;

    private NpgsqlConnection Create() =>
        new(_cs.PgPhMetaDb ?? throw new InvalidOperationException("ConnectionStrings:PgPhMetaDb is not configured"));

    public async Task<IReadOnlyList<BackupStatusRow>> GetAllAsync(CancellationToken ct = default)
    {
        var list = new List<BackupStatusRow>();
        await using var conn = Create();
        await conn.OpenAsync(ct);
        var sql = "SELECT partition, folder, activity_kind, status, error_message FROM backup_status";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            list.Add(new BackupStatusRow(
                rdr.GetString(0),
                rdr.GetString(1),
                rdr.GetString(2),
                rdr.GetString(3),
                rdr.IsDBNull(4) ? null : rdr.GetString(4)
            ));
        }
        return list;
    }

    public async Task TruncateAsync(CancellationToken ct = default)
    {
        await using var conn = Create();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("TRUNCATE TABLE backup_status", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
