using Npgsql;
using NpgsqlTypes;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace meta_uploader;

public class RegisteredObjectUploader
{
    private readonly IContentProvider _contentProvider;
    private readonly string _connectionString;
    private const int BatchSize = 200;
    private readonly List<ObjectRecord> _buffer = new();
    private readonly HashSet<string> _objectsInDb = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Func<string, Task<IEnumerable<ObjectRecord>>>> _collectors = new();

    public RegisteredObjectUploader(IContentProvider contentProvider)
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

        InitializeCollectors();
    }

    private void InitializeCollectors()
    {
        // Primary
        AddCollector(async md5 => new[] { new ObjectRecord(EnsureMd5(md5), "primary") });

        // Previews
        AddCollector(async md5 =>
        {
            var metadata = await _contentProvider.GetMetadataWithPreviews(md5);
            return metadata?.Previews?.Select(p => new ObjectRecord(EnsureMd5(p.Value), "preview")) ?? Array.Empty<ObjectRecord>();
        });

        // Answers
        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetEmbAnswer(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "emb_answer") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetDqAnswer(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "dq_answer") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetCommerceMarkAnswer(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "commerce_mark_answer") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetEngShortAnswer(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "eng_short_answer") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetEng30TagsAnswer(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "eng_30_tags_answer") };
        });

        // Questions
        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetDqQuestion(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "dq_question") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetCommerceMarkQuestion(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "commerce_mark_question") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetEng30TagsQuestion(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "eng_30_tags_question") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetEngShortQuestion(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "eng_short_question") };
        });

        // Conversations
        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetEmbConversation(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "emb_conversation") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetDqConversation(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "dq_conversation") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetCommerceMarkConversation(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "commerce_mark_conversation") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetEng30TagsConversation(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "eng_30_tags_conversation") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetEngShortConversation(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "eng_short_conversation") };
        });

        // Other metadata
        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetDescription(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "description") };
        });

        AddCollector(async md5 =>
        {
            var tags = await _contentProvider.GetTags(md5);
            if (tags == null || tags.Length == 0) return Array.Empty<ObjectRecord>();
            var result = string.Join(", ", tags);
            return new[] { new ObjectRecord(result.AsMd5(), "tags") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetAverageHash(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "average_hash") };
        });

        AddCollector(async md5 =>
        {
            var result = await _contentProvider.GetColorHash(md5);
            return string.IsNullOrEmpty(result) ? Array.Empty<ObjectRecord>() : new[] { new ObjectRecord(result.AsMd5(), "color_hash") };
        });

        AddCollector(async md5 =>
        {
            var tags = await _contentProvider.GetEng30Tags(md5);
            if (tags == null || tags.Length == 0) return Array.Empty<ObjectRecord>();
            var result = string.Join(", ", tags);
            return new[] { new ObjectRecord(result.AsMd5(), "eng_30_tags") };
        });

        AddCollector(async md5 =>
        {
            var commerceJson = await _contentProvider.GetCommerceMarkAnswerJson(md5);
            if (commerceJson == null) return Array.Empty<ObjectRecord>();
            var result = Newtonsoft.Json.JsonConvert.SerializeObject(commerceJson);
            return new[] { new ObjectRecord(result.AsMd5(), "commerce_mark_answer_json") };
        });
    }

    public void AddCollector(Func<string, Task<IEnumerable<ObjectRecord>>> collector)
    {
        _collectors.Add(collector);
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
            var records = await CollectRecords(md5);
            foreach (var record in records)
            {
                if (string.IsNullOrEmpty(record.md5_hash)) continue;
                
                if (!_objectsInDb.Contains(record.md5_hash))
                {
                    _buffer.Add(record);
                    _objectsInDb.Add(record.md5_hash); // prevent duplicates in the same run/buffer
                }
            }

            if (_buffer.Count >= BatchSize)
            {
                Console.WriteLine($"Writing {_buffer.Count} objects to DB");
                await UpsertBatchAsync(_buffer);
                Console.WriteLine($"Done {_buffer.Count} writing objects to DB");
                _buffer.Clear();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing object '{md5}': {ex.Message}");
        }
    }

    public async Task<List<ObjectRecord>> CollectRecords(string md5)
    {
        var results = new List<ObjectRecord>();
        foreach (var collector in _collectors)
        {
            try
            {
                results.AddRange(await collector(md5));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Collector error for {md5}: {ex.Message}");
            }
        }
        return results;
    }

    private async Task LoadExistingMd5Async()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT md5_hash FROM registered_objects", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var hash = reader.GetString(0);
            _objectsInDb.Add(hash);
        }
    }

    private async Task UpsertBatchAsync(List<ObjectRecord> batch)
    {
        Console.WriteLine($"Upserting {batch.Count} records into registered_objects...");
        if (batch.Count == 0) return;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var sb = new System.Text.StringBuilder();
        sb.Append("INSERT INTO registered_objects (md5_hash, object_type) VALUES ");

        var cmd = new NpgsqlCommand();
        cmd.Connection = conn;
        cmd.Transaction = tx;

        for (int i = 0; i < batch.Count; i++)
        {
            var r = batch[i];
            if (i > 0) sb.Append(",");
            sb.Append($"(@md5_{i}, @type_{i})");

            cmd.Parameters.AddWithValue($"@md5_{i}", NpgsqlDbType.Text, r.md5_hash);
            cmd.Parameters.AddWithValue($"@type_{i}", NpgsqlDbType.Text, r.object_type);
        }

        sb.Append(" ON CONFLICT (md5_hash) DO UPDATE SET object_type = EXCLUDED.object_type;");

        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    private string EnsureMd5(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        if (CalculationExtensions.Md5PrefixRegex.IsMatch(input)) return input;
        return input.AsMd5();
    }

    public sealed record ObjectRecord(
        string md5_hash,
        string object_type
    );
}
