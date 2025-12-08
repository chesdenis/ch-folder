using Npgsql;
using NpgsqlTypes;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace meta_uploader;

public class MetaUploader
{
    private readonly IFileSystem _fileSystem;
    private readonly IFileHasher _fileHasher;
    private readonly string _connectionString;
    private const int BatchSize = 200;
    private readonly List<PhotoRecord> _buffer = new();
    private readonly HashSet<string> _existingMd5 = new(StringComparer.OrdinalIgnoreCase);

    public MetaUploader(IFileSystem fileSystem, IFileHasher fileHasher)
    {
        _fileSystem = fileSystem;
        _fileHasher = fileHasher;
        _connectionString =
            string.Join(";",
                $"Host={Environment.GetEnvironmentVariable("Host")}",
                $"Port={Environment.GetEnvironmentVariable("Port")}",
                $"Database={Environment.GetEnvironmentVariable("Database")}",
                $"Username={Environment.GetEnvironmentVariable("Username")}",
                $"Password={Environment.GetEnvironmentVariable("Password")}",
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
        await _fileSystem.WalkThrough(args, ProcessSingleFile);

        // flush remaining buffer
        if (_buffer.Count > 0)
        {
            await UpsertBatchAsync(_buffer);
            _buffer.Clear();
        }
    }

    private async Task ProcessSingleFile(string filePath)
    {
        if (!filePath.AllowImageToProcess())
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            var fileName = Path.GetFileName(filePath);
            var dirName = string.IsNullOrEmpty(directory) ? string.Empty : Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
            var sizeBytes = new FileInfo(filePath).Length;

            // compute md5 from file content
            var md5 = await _fileHasher.ComputeMd5Async(filePath);

            // skip if already in DB
            if (_existingMd5.Contains(md5))
            {
                return;
            }

            var record = new PhotoRecord(
                md5_hash: md5,
                file_name: fileName,
                dir_name: dirName,
                extension: extension,
                dir_path: directory,
                file_path: Path.GetFullPath(filePath),
                size_bytes: sizeBytes,
                tags: Array.Empty<string>(),
                short_details: Path.GetFileNameWithoutExtension(fileName),
                // TODO: populate color and average hashes
                color_hash: "na",
                average_hash: "na"
            );

            _buffer.Add(record);
            if (_buffer.Count >= BatchSize)
            {
                await UpsertBatchAsync(_buffer);
                _buffer.Clear();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file '{filePath}': {ex.Message}");
        }
    }

    private async Task LoadExistingMd5Async()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT md5_hash FROM photo", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var hash = reader.GetString(0);
            _existingMd5.Add(hash);
        }
    }

    private async Task UpsertBatchAsync(List<PhotoRecord> batch)
    {
        Console.WriteLine($"Upserting {batch.Count} records...");
        if (batch.Count == 0) return;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // Build SQL with parameterized multi-values
        var sb = new System.Text.StringBuilder();
        sb.Append("INSERT INTO photo (md5_hash, file_name, dir_name, extension, dir_path, file_path, size_bytes, tags, short_details, color_hash, average_hash) VALUES ");

        var cmd = new NpgsqlCommand();
        cmd.Connection = conn;
        cmd.Transaction = tx;

        for (int i = 0; i < batch.Count; i++)
        {
            var r = batch[i];
            if (i > 0) sb.Append(",");
            sb.Append($"(@md5_{i}, @fn_{i}, @dn_{i}, @ext_{i}, @dpath_{i}, @fpath_{i}, @sz_{i}, @tags_{i}, @sd_{i}, @ch_{i}, @ah_{i})");

            cmd.Parameters.AddWithValue($"@md5_{i}", NpgsqlDbType.Text, r.md5_hash);
            cmd.Parameters.AddWithValue($"@fn_{i}", NpgsqlDbType.Text, r.file_name);
            cmd.Parameters.AddWithValue($"@dn_{i}", NpgsqlDbType.Text, r.dir_name);
            cmd.Parameters.AddWithValue($"@ext_{i}", NpgsqlDbType.Text, r.extension);
            cmd.Parameters.AddWithValue($"@dpath_{i}", NpgsqlDbType.Text, r.dir_path);
            cmd.Parameters.AddWithValue($"@fpath_{i}", NpgsqlDbType.Text, r.file_path);
            cmd.Parameters.AddWithValue($"@sz_{i}", NpgsqlDbType.Bigint, r.size_bytes);
            var pTags = new NpgsqlParameter<string[]>($"@tags_{i}", NpgsqlDbType.Array | NpgsqlDbType.Text) { TypedValue = r.tags };
            cmd.Parameters.Add(pTags);
            cmd.Parameters.AddWithValue($"@sd_{i}", NpgsqlDbType.Text, r.short_details);
            cmd.Parameters.AddWithValue($"@ch_{i}", NpgsqlDbType.Text, r.color_hash);
            cmd.Parameters.AddWithValue($"@ah_{i}", NpgsqlDbType.Text, r.average_hash);
        }

        sb.Append(" ON CONFLICT (md5_hash) DO UPDATE SET ");
        sb.Append("file_name = EXCLUDED.file_name, ");
        sb.Append("dir_name = EXCLUDED.dir_name, ");
        sb.Append("extension = EXCLUDED.extension, ");
        sb.Append("dir_path = EXCLUDED.dir_path, ");
        sb.Append("file_path = EXCLUDED.file_path, ");
        sb.Append("size_bytes = EXCLUDED.size_bytes, ");
        sb.Append("tags = EXCLUDED.tags, ");
        sb.Append("short_details = EXCLUDED.short_details, ");
        sb.Append("color_hash = EXCLUDED.color_hash, ");
        sb.Append("average_hash = EXCLUDED.average_hash;");

        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    private sealed record PhotoRecord(
        string md5_hash,
        string file_name,
        string dir_name,
        string extension,
        string dir_path,
        string file_path,
        long size_bytes,
        string[] tags,
        string short_details,
        string color_hash,
        string average_hash
    );
}