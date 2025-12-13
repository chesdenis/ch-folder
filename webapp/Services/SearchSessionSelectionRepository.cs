using Microsoft.Extensions.Options;
using Npgsql;
using webapp.Models;

namespace webapp.Services;

public interface ISearchSessionSelectionRepository
{
    Task<IReadOnlyList<string>> GetSelectedMd5Async(Guid sessionId, CancellationToken ct = default);
    Task<bool> AddSelectionAsync(Guid sessionId, string md5, CancellationToken ct = default);
    Task<bool> RemoveSelectionAsync(Guid sessionId, string md5, CancellationToken ct = default);
}

public sealed class SearchSessionSelectionRepository(IOptions<ConnectionStringOptions> connectionStringsOptions)
    : ISearchSessionSelectionRepository
{
    private readonly ConnectionStringOptions _connectionStrings = connectionStringsOptions.Value;

    private NpgsqlConnection CreateConnection() =>
        new(_connectionStrings.PgPhMetaDb ?? throw new InvalidOperationException());

    public async Task<IReadOnlyList<string>> GetSelectedMd5Async(Guid sessionId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var list = new List<string>();
        await using var cmd = new NpgsqlCommand(@"SELECT md5_hash
            FROM search_session_selected
            WHERE session_id = @sid
            ORDER BY created_at ASC", conn);
        cmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(reader.GetString(0));
        }

        return list;
    }

    public async Task<bool> AddSelectionAsync(Guid sessionId, string md5, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(@"INSERT INTO search_session_selected(session_id, md5_hash)
            VALUES (@sid, @md5)
            ON CONFLICT DO NOTHING", conn);
        cmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId);
        cmd.Parameters.AddWithValue("@md5", NpgsqlTypes.NpgsqlDbType.Text, md5);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0;
    }

    public async Task<bool> RemoveSelectionAsync(Guid sessionId, string md5, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(@"DELETE FROM search_session_selected
            WHERE session_id = @sid AND md5_hash = @md5", conn);
        cmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId);
        cmd.Parameters.AddWithValue("@md5", NpgsqlTypes.NpgsqlDbType.Text, md5);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0;
    }
}
