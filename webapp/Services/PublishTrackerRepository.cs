using Npgsql;
using Microsoft.Extensions.Options;
using webapp.Models;

namespace webapp.Services;

public interface IPublishTrackerRepository
{
    Task TogglePublishStatusAsync(string md5, string platform, CancellationToken ct = default);
    Task<Dictionary<string, string[]>> GetPublishStatusesAsync(IEnumerable<string> md5s, CancellationToken ct = default);
}

public sealed class PublishTrackerRepository(IOptions<ConnectionStringOptions> connectionStringsOptions) : IPublishTrackerRepository
{
    private readonly ConnectionStringOptions _connectionStrings = connectionStringsOptions.Value;

    private NpgsqlConnection CreateConnection() =>
        new(_connectionStrings.PgPhMetaDb ?? throw new InvalidOperationException());

    public async Task TogglePublishStatusAsync(string md5, string platform, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // Check if exists
        await using var checkCmd = new NpgsqlCommand("SELECT 1 FROM photo_publish_tracker WHERE md5_hash = @md5 AND platform = @platform", conn);
        checkCmd.Parameters.AddWithValue("@md5", md5);
        checkCmd.Parameters.AddWithValue("@platform", platform);
        
        var exists = await checkCmd.ExecuteScalarAsync(ct) != null;

        if (exists)
        {
            await using var deleteCmd = new NpgsqlCommand("DELETE FROM photo_publish_tracker WHERE md5_hash = @md5 AND platform = @platform", conn);
            deleteCmd.Parameters.AddWithValue("@md5", md5);
            deleteCmd.Parameters.AddWithValue("@platform", platform);
            await deleteCmd.ExecuteNonQueryAsync(ct);
        }
        else
        {
            await using var insertCmd = new NpgsqlCommand("INSERT INTO photo_publish_tracker (md5_hash, platform) VALUES (@md5, @platform) ON CONFLICT DO NOTHING", conn);
            insertCmd.Parameters.AddWithValue("@md5", md5);
            insertCmd.Parameters.AddWithValue("@platform", platform);
            await insertCmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<Dictionary<string, string[]>> GetPublishStatusesAsync(IEnumerable<string> md5s, CancellationToken ct = default)
    {
        var md5Array = md5s.ToArray();
        if (md5Array.Length == 0) return new Dictionary<string, string[]>();

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var result = new Dictionary<string, string[]>();
        await using var cmd = new NpgsqlCommand("SELECT md5_hash, array_agg(platform) FROM photo_publish_tracker WHERE md5_hash = ANY(@md5s) GROUP BY md5_hash", conn);
        cmd.Parameters.AddWithValue("@md5s", md5Array);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result[reader.GetString(0)] = reader.GetFieldValue<string[]>(1);
        }

        return result;
    }
}
