using Npgsql;
using NpgsqlTypes;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace meta_uploader;

public class ImageMetaUploader
{
    private readonly IFileSystem _fileSystem;
    private readonly IFileHasher _fileHasher;
    private readonly string _connectionString;
    private const int BatchSize = 200;
    private readonly List<PhotoRecord> _buffer = new();
    private readonly HashSet<string> _existingMd5 = new(StringComparer.OrdinalIgnoreCase);

    public ImageMetaUploader(IFileSystem fileSystem, IFileHasher fileHasher)
    {
        _fileSystem = fileSystem;
        _fileHasher = fileHasher;
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
            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
            var sizeBytes = new FileInfo(filePath).Length;

            // compute md5 from file content
            var md5 = await _fileHasher.ComputeMd5Async(filePath);

            // skip if already in DB
            if (_existingMd5.Contains(md5))
            {
                return;
            }
            
            if (!_fileSystem.FileExists(PathExtensions.ResolveEmbAnswer(filePath))) return;
            if (!_fileSystem.FileExists(PathExtensions.ResolveDqAnswerPath(filePath))) return;
            if (!_fileSystem.FileExists(PathExtensions.ResolveEngShortAnswerPath(filePath))) return;
            if (!_fileSystem.FileExists(PathExtensions.ResolveCommerceMarkAnswerPath(filePath))) return;
            if (!_fileSystem.FileExists(PathExtensions.ResolveEng30TagsAnswerPath(filePath))) return;


            // try read commerce rate explanation
            int commerceRate = 0;
            try
            {
                var rate = await ImageProcessingExtensions.GetRateExplanation(filePath);
                if (rate != null)
                {
                    // DB constraint currently allows 0..5
                    commerceRate = Math.Max(0, Math.Min(5, rate.rate));
                }
            }
            catch
            {
                // ignore missing/invalid files, keep default 0
            }
            
            // try read faces information
            string[] persons = Array.Empty<string>();
            try
            {
                persons = ImageProcessingExtensions.GetFacesOnPhotos(filePath);
            }
            catch  
            {
                // ignore missing/invalid files
            }

            var eng30TagsText = ImageProcessingExtensions.GetEng30TagsText(filePath);
            var shortDetails = ImageProcessingExtensions.GetEngShortText(filePath);
            
            var record = new PhotoRecord(
                md5_hash: md5,
                extension: extension,
                size_bytes: sizeBytes,
                tags: eng30TagsText,
                persons: persons,
                short_details:  shortDetails,
                commerce_rate: commerceRate);

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
        sb.Append("INSERT INTO photo (")
            .Append("md5_hash, ")
            .Append("extension, ")
            .Append("size_bytes, ")
            .Append("tags, ")
            .Append("persons, ")
            .Append("short_details, ")
            .Append("commerce_rate) VALUES ");

        var cmd = new NpgsqlCommand();
        cmd.Connection = conn;
        cmd.Transaction = tx;

        for (int i = 0; i < batch.Count; i++)
        {
            var r = batch[i];
            if (i > 0) sb.Append(",");
            sb.Append($"(@md5_{i}, " +
                      $"@ext_{i}, " +
                      $"@sz_{i}, " +
                      $"@tags_{i}, " +
                      $"@persons_{i}, " +
                      $"@sd_{i}, " +
                      $"@cr_{i})");

            cmd.Parameters.AddWithValue($"@md5_{i}", NpgsqlDbType.Text, r.md5_hash);
            cmd.Parameters.AddWithValue($"@ext_{i}", NpgsqlDbType.Text, r.extension);
            cmd.Parameters.AddWithValue($"@sz_{i}", NpgsqlDbType.Bigint, r.size_bytes);
            var pTags = new NpgsqlParameter<string[]>($"@tags_{i}", NpgsqlDbType.Array | NpgsqlDbType.Text) { TypedValue = r.tags };
            cmd.Parameters.Add(pTags);
            var pPersons = new NpgsqlParameter<string[]>($"@persons_{i}", NpgsqlDbType.Array | NpgsqlDbType.Text) { TypedValue = r.persons };
            cmd.Parameters.Add(pPersons);
            cmd.Parameters.AddWithValue($"@sd_{i}", NpgsqlDbType.Text, r.short_details);
            cmd.Parameters.AddWithValue($"@cr_{i}", NpgsqlDbType.Integer, r.commerce_rate);
        }

        sb.Append(" ON CONFLICT (md5_hash) DO UPDATE SET ");
        sb.Append("extension = EXCLUDED.extension, ");
        sb.Append("size_bytes = EXCLUDED.size_bytes, ");
        sb.Append("tags = EXCLUDED.tags, ");
        sb.Append("persons = EXCLUDED.persons, ");
        sb.Append("short_details = EXCLUDED.short_details, ");
        sb.Append("commerce_rate = EXCLUDED.commerce_rate;");

        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    private sealed record PhotoRecord(
        string md5_hash,
        string extension,
        long size_bytes,
        string[] tags,
        string[] persons,
        string short_details,
        int commerce_rate
    );
}