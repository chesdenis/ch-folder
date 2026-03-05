using Npgsql;
using NpgsqlTypes;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace meta_uploader;

public class FileMetaUploader
{
    private readonly IContentProvider _contentProvider;
    private readonly string _connectionString;
    private const int BatchSize = 200;
    private readonly List<FileRecord> _buffer = new();
    private readonly HashSet<string> _filePointersInDb = new(StringComparer.OrdinalIgnoreCase);

    public FileMetaUploader(IContentProvider contentProvider)
    {
        _contentProvider = contentProvider;
        _connectionString =
            string.Join(";",
                $"Host={Environment.GetEnvironmentVariable("PG_HOST")}",
                $"Port={Environment.GetEnvironmentVariable("PG_PORT")}",
                $"Database={Environment.GetEnvironmentVariable("PG_DATABASE")}",
                $"Username={Environment.GetEnvironmentVariable("PG_USERNAME")}",
                $"Password={Environment.GetEnvironmentVariable("PG_PASSWORD")}",
                "Ssl Mode=Disable",
                "Trust Server Certificate=true",
                "Include Error Detail=true"
            );
        _connectionString = _connectionString ?? throw new ArgumentNullException(nameof(_connectionString));
    }
    
    public async Task RunAsync(string[] args)
    {
        args = args.ValidateArgs();
        await LoadExistingMd5Async();
        await _contentProvider.WalkThrough(args, ProcessSingleFile);

        // flush remaining buffer
        if (_buffer.Count > 0)
        {
            await UpsertBatchAsync(_buffer);
            _buffer.Clear();
        }
    }

    private async Task ProcessSingleFile(string md5)
    {
        try
        {
            // skip if already in DB
            if (_filePointersInDb.Contains(md5))
            {
                return;
            }
            
            var metadata = await _contentProvider.GetMetadataByMd5(md5);
            if (metadata == null) return;

            var extension = await _contentProvider.GetExtension(md5);
            var partition = await _contentProvider.GetPartition(md5);
            var section = await _contentProvider.GetSection(md5);
            
            var record = new FileRecord(
                md5_hash: md5,
                extension: extension,
                partition: partition,
                section: section);

            _buffer.Add(record);
            if (_buffer.Count >= BatchSize)
            {
                Console.WriteLine($"Writing {_buffer.Count} to DB");
                await UpsertBatchAsync(_buffer);
                Console.WriteLine($"Done {_buffer.Count} writing to DB");
                _buffer.Clear();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file '{md5}': {ex.Message}");
        }
    }

    private async Task LoadExistingMd5Async()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT md5_hash FROM files", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var hash = reader.GetString(0);
            _filePointersInDb.Add(hash);
        }
    }

    private async Task UpsertBatchAsync(List<FileRecord> batch)
    {
        Console.WriteLine($"Upserting {batch.Count} records into files...");
        if (batch.Count == 0) return;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // Build SQL with parameterized multi-values
        var sb = new System.Text.StringBuilder();
        sb.Append("INSERT INTO files (")
            .Append("md5_hash, ")
            .Append("extension, ")
            .Append("\"partition\", ")
            .Append("\"section\") VALUES ");

        var cmd = new NpgsqlCommand();
        cmd.Connection = conn;
        cmd.Transaction = tx;

        for (int i = 0; i < batch.Count; i++)
        {
            var r = batch[i];
            if (i > 0) sb.Append(",");
            sb.Append($"(@md5_{i}, " +
                      $"@ext_{i}, " +
                      $"@p_{i}, " +
                      $"@s_{i})");

            cmd.Parameters.AddWithValue($"@md5_{i}", NpgsqlDbType.Text, r.md5_hash);
            cmd.Parameters.AddWithValue($"@ext_{i}", NpgsqlDbType.Text, r.extension);
            cmd.Parameters.AddWithValue($"@p_{i}", NpgsqlDbType.Text, r.partition);
            cmd.Parameters.AddWithValue($"@s_{i}", NpgsqlDbType.Text, r.section);
        }

        sb.Append(" ON CONFLICT (md5_hash) DO UPDATE SET ");
        sb.Append("extension = EXCLUDED.extension, ");
        sb.Append("\"partition\" = EXCLUDED.\"partition\", ");
        sb.Append("\"section\" = EXCLUDED.\"section\";");

        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    private sealed record FileRecord(
        string md5_hash,
        string extension,
        string partition,
        string section
    );
}
