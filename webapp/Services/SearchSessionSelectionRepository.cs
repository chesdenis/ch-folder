using Microsoft.Extensions.Options;
using Npgsql;
using webapp.Models;

namespace webapp.Services;

public interface ISearchSessionSelectionRepository
{
    Task<IReadOnlyList<SelectedPhotoInfo>> GetSelectedMd5Async(Guid sessionId, CancellationToken ct = default);
    Task<bool> AddSelectionAsync(Guid sessionId, string md5, CancellationToken ct = default);
    Task<bool> RemoveSelectionAsync(Guid sessionId, string md5, CancellationToken ct = default);
}

public sealed class SearchSessionSelectionRepository(IOptions<ConnectionStringOptions> connectionStringsOptions)
    : ISearchSessionSelectionRepository
{
    private readonly ConnectionStringOptions _connectionStrings = connectionStringsOptions.Value;

    private NpgsqlConnection CreateConnection() =>
        new(_connectionStrings.PgPhMetaDb ?? throw new InvalidOperationException());

    public async Task<IReadOnlyList<SelectedPhotoInfo>> GetSelectedMd5Async(Guid sessionId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var list = new List<SelectedPhotoInfo>();
        await using var cmd = new NpgsqlCommand(@"SELECT p.md5_hash, p.short_details, p.tags
            FROM search_session_selected s
            INNER JOIN photo p ON p.md5_hash = s.md5_hash
            WHERE s.session_id = @sid
            ORDER BY s.created_at ASC", conn);
        cmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var md5 = reader.GetString(0);
            var shortDetails = reader.GetString(1);
            var tags = reader.IsDBNull(2) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(2);
            list.Add(new SelectedPhotoInfo(md5, shortDetails, tags));
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

public sealed record SelectedPhotoInfo(string Md5, string ShortDetails, string[] Tags);
