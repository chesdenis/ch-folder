using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using webapp.Models;

namespace webapp.Services;

public interface IImageLocationRepository
{
    Task UpsertLocationsAsync(IEnumerable<KeyValuePair<string, string>> md5ToPath, CancellationToken ct = default);
    Task<string?> GetPathByMd5Async(string md5, CancellationToken ct = default);
    Task<IDictionary<string, string>> GetAllAsync(CancellationToken ct = default);
    Task DeleteMissingLocationsAsync(IEnumerable<string> existingMd5s, CancellationToken ct = default);
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

    public async Task<string?> GetPathByMd5Async(string md5, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT real_path FROM image_location WHERE md5_hash = @md5 LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@md5", NpgsqlDbType.Text, md5);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result?.ToString();
    }
    
    public async Task<IDictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        var dict = new Dictionary<string, string>();
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT md5_hash, real_path FROM image_location", conn);
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
        {
            dict[rdr.GetString(0)] = rdr.GetString(1);
        }
        return dict;
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

    public async Task DeleteMissingLocationsAsync(IEnumerable<string> existingMd5s, CancellationToken ct = default)
    {
        var existingMd5Set = existingMd5s.ToHashSet();
        
        // Fetch all MD5s from the database
        var allDbMd5s = new List<string>();
        await using (var conn = CreateConnection())
        {
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT md5_hash FROM image_location", conn);
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                allDbMd5s.Add(rdr.GetString(0));
            }
        }

        var toDelete = allDbMd5s.Where(md5 => !existingMd5Set.Contains(md5)).ToList();
        
        if (toDelete.Count == 0) return;

        logger.LogInformation("ImageLocationRepository: found {Count} dead links to remove", toDelete.Count);

        // Delete in batches to avoid locking or huge commands
        for (var offset = 0; offset < toDelete.Count; offset += BatchSize)
        {
            var batch = toDelete.Skip(offset).Take(BatchSize).ToList();
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);
            
            var sb = new System.Text.StringBuilder();
            sb.Append("DELETE FROM image_location WHERE md5_hash IN (");
            await using var cmd = new NpgsqlCommand { Connection = conn, Transaction = tx };
            
            for (int i = 0; i < batch.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"@md5_{i}");
                cmd.Parameters.AddWithValue($"@md5_{i}", NpgsqlDbType.Text, batch[i]);
            }
            sb.Append(")");
            
            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
        }
        
        logger.LogInformation("ImageLocationRepository: deleted {Count} dead links from image_location", toDelete.Count);
    }
}
