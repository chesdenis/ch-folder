using Microsoft.Extensions.Options;
using Npgsql;
using shared_csharp.Abstractions;
using webapp.Models;

namespace webapp.Services;

public interface ISearchSessionSelectionRepository
{
    Task<IReadOnlyList<SelectedPhotoInfo>> GetSelectedMd5Async(Guid sessionId, CancellationToken ct = default);
    Task<bool> AddSelectionAsync(Guid sessionId, string md5, CancellationToken ct = default);
    Task<bool> RemoveSelectionAsync(Guid sessionId, string md5, CancellationToken ct = default);
    Task ClearSelectionAsync(Guid sessionId, CancellationToken ct = default);
    Task<Guid> CreateSelectionSessionAsync(Guid sourceSessionId, string queryText, CancellationToken ct = default);
    Task<(IReadOnlyList<SearchSessionRow> Items, int Total)> GetRecentSelectionSessionsAsync(int offset, int limit, CancellationToken ct = default);
}

public sealed class SearchSessionSelectionRepository(IContentProvider contentProvider, IOptions<ConnectionStringOptions> connectionStringsOptions)
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
            FROM (
                SELECT md5_hash, created_at FROM search_session_selected WHERE session_id = @sid
                UNION ALL
                SELECT md5_hash, created_at FROM selection_session_photo WHERE session_id = @sid
            ) s
            INNER JOIN photo p ON p.md5_hash = s.md5_hash
            ORDER BY s.created_at ASC", conn);
        cmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var md5 = reader.GetString(0);
            var shortDetails = reader.GetString(1);
            var largeDetails = await contentProvider.GetDqAnswer(md5);
            var commerceMark = await contentProvider.GetCommerceMarkAnswer(md5);
            var tags = reader.IsDBNull(2) ? Array.Empty<string>() : reader.GetFieldValue<string[]>(2);

            // Fetch publish platforms for this photo
            var platforms = new List<string>();
            await using (var pConn = CreateConnection())
            {
                await pConn.OpenAsync(ct);
                await using var pCmd = new NpgsqlCommand("SELECT platform FROM photo_publish_tracker WHERE md5_hash = @md5", pConn);
                pCmd.Parameters.AddWithValue("@md5", md5);
                await using var pReader = await pCmd.ExecuteReaderAsync(ct);
                while (await pReader.ReadAsync(ct))
                {
                    platforms.Add(pReader.GetString(0));
                }
            }

            list.Add(new SelectedPhotoInfo(md5, shortDetails, largeDetails, commerceMark, tags, platforms.ToArray()));
        }

        return list;
    }

    public async Task<bool> AddSelectionAsync(Guid sessionId, string md5, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        if (sessionId == Guid.Empty)
        {
            // Ensure static navigation session exists in search_session table to satisfy FK
            await using var cmdS = new NpgsqlCommand(@"INSERT INTO search_session
                (id, query_text, embedding_model, embedding_dim, collection_name, limit_requested, result_count)
                VALUES (@id, 'Navigation', 'none', 1, 'none', 1, 0)
                ON CONFLICT DO NOTHING", conn);
            cmdS.Parameters.AddWithValue("@id", NpgsqlTypes.NpgsqlDbType.Uuid, Guid.Empty);
            await cmdS.ExecuteNonQueryAsync(ct);
        }

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

    public async Task ClearSelectionAsync(Guid sessionId, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(@"DELETE FROM search_session_selected
            WHERE session_id = @sid", conn);
        cmd.Parameters.AddWithValue("@sid", NpgsqlTypes.NpgsqlDbType.Uuid, sessionId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Guid> CreateSelectionSessionAsync(Guid sourceSessionId, string queryText, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var newSessionId = Guid.NewGuid();

            // 1. Create a new selection_session entry
            await using (var cmd = new NpgsqlCommand(@"INSERT INTO selection_session
                (id, created_at, name, item_count)
                VALUES (@id, now(), @name, 
                       (SELECT COUNT(1) FROM search_session_selected WHERE session_id = @sid))", conn, tx))
            {
                cmd.Parameters.AddWithValue("@id", newSessionId);
                cmd.Parameters.AddWithValue("@name", queryText);
                cmd.Parameters.AddWithValue("@sid", sourceSessionId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // 2. Copy selected items from source session to selection_session_photo
            await using (var cmd = new NpgsqlCommand(@"INSERT INTO selection_session_photo (session_id, md5_hash, created_at)
                SELECT @newId, md5_hash, created_at
                FROM search_session_selected
                WHERE session_id = @sid", conn, tx))
            {
                cmd.Parameters.AddWithValue("@newId", newSessionId);
                cmd.Parameters.AddWithValue("@sid", sourceSessionId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return newSessionId;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<(IReadOnlyList<SearchSessionRow> Items, int Total)> GetRecentSelectionSessionsAsync(int offset, int limit, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        // total count of selection sessions
        int total;
        await using (var countCmd = new NpgsqlCommand("SELECT COUNT(1) FROM selection_session", conn))
        {
            var scalar = await countCmd.ExecuteScalarAsync(ct);
            total = Convert.ToInt32(scalar);
        }

        var list = new List<SearchSessionRow>(Math.Max(0, limit));
        await using (var cmd = new NpgsqlCommand(@"SELECT id, created_at, name, item_count
            FROM selection_session
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
                    EmbeddingModel = "selection",
                    ResultCount = reader.GetInt32(3),
                    ScoreThreshold = null
                });
            }
        }

        return (list, total);
    }
}

public sealed record SelectedPhotoInfo(string Md5, string ShortDetails, string LargeDetails, string CommerceMark, string[] Tags, string[] PublishPlatforms);
