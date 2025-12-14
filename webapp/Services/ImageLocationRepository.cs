using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using webapp.Models;

namespace webapp.Services;

public interface IImageLocationRepository
{
    Task UpsertLocationsAsync(IEnumerable<KeyValuePair<string, string>> md5ToPath, CancellationToken ct = default);
}

public sealed class ImageLocationRepository(IOptions<ConnectionStringOptions> connectionStringsOptions, ILogger<ImageLocationRepository> logger)
    : IImageLocationRepository
{
    private readonly ConnectionStringOptions _connectionStrings = connectionStringsOptions.Value;
    private const int BatchSize = 200;

    private NpgsqlConnection CreateConnection() =>
        new(_connectionStrings.PgPhMetaDb ?? throw new InvalidOperationException("ConnectionStrings:PgPhMetaDb is not configured"));

    public async Task UpsertLocationsAsync(IEnumerable<KeyValuePair<string, string>> md5ToPath, CancellationToken ct = default)
    {
        // Materialize to avoid multiple enumeration and allow batching
        var list = md5ToPath as IList<KeyValuePair<string, string>> ?? md5ToPath.ToList();
        if (list.Count == 0) return;

        for (var offset = 0; offset < list.Count; offset += BatchSize)
        {
            var batch = list.Skip(offset).Take(BatchSize).ToList();
            await UpsertBatchAsync(batch, ct);
        }

        logger.LogInformation("ImageLocationRepository: upserted {Count} records into image_location table", list.Count);
    }

    private async Task UpsertBatchAsync(IReadOnlyList<KeyValuePair<string, string>> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var sb = new System.Text.StringBuilder();
        sb.Append("INSERT INTO image_location (md5_hash, real_path) VALUES ");

        await using var cmd = new NpgsqlCommand { Connection = conn, Transaction = tx };

        for (int i = 0; i < batch.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append($"(@md5_{i}, @path_{i})");
            var kv = batch[i];
            cmd.Parameters.AddWithValue($"@md5_{i}", NpgsqlDbType.Text, kv.Key);
            cmd.Parameters.AddWithValue($"@path_{i}", NpgsqlDbType.Text, kv.Value);
        }

        sb.Append(" ON CONFLICT (md5_hash) DO UPDATE SET real_path = EXCLUDED.real_path;");

        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }
}
