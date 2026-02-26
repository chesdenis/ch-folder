using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using NpgsqlTypes;
using shared_csharp;
using shared_csharp.Extensions;

namespace image_searcher;

public class ImageSearcher
{
    private const string Collection = "photos";

    private readonly string _qdrantConnectionString;
    private readonly string _pgConnectionString;

    private readonly string _openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                                         ?? throw new ArgumentNullException(nameof(_openAiKey));

    private const string Url = "https://api.openai.com/v1/embeddings";
    private const string Model = "text-embedding-ada-002";

    public ImageSearcher()
    {
        _qdrantConnectionString =
            $"http://{Environment.GetEnvironmentVariable("QD_HOST")}:{Environment.GetEnvironmentVariable("QD_PORT")}";
        _pgConnectionString = string.Join(";",
            $"Host={Environment.GetEnvironmentVariable("PG_HOST")}",
            $"Port={Environment.GetEnvironmentVariable("PG_PORT")}",
            $"Database={Environment.GetEnvironmentVariable("PG_DATABASE")}",
            $"Username={Environment.GetEnvironmentVariable("PG_USERNAME")}",
            $"Password={Environment.GetEnvironmentVariable("PG_PASSWORD")}",
            "Ssl Mode=Disable",
            "Trust Server Certificate=true",
            "Include Error Detail=true"
        );
    }

    public async Task RunAsync(string[] args)
    {
        string queryText = args[0];

        var response = await GetOpenAiEmbedding(queryText);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var queryEmbedding = JsonSerializer.Deserialize<EmbeddingFile>(response, options);
        if (queryEmbedding?.data == null || queryEmbedding.data.Count == 0)
        {
            return;
        }

        var queryVector = queryEmbedding.data[0].embedding.ToArray();

        using var http = new HttpClient { BaseAddress = new Uri(_qdrantConnectionString) };

        // Build optional pre-filter from args
        var filter = BuildFilter(args.Skip(1).ToArray());

        var req = new SearchRequest(
            Vector: queryVector,
            Limit: 100,
            WithPayload: true,
            ScoreThreshold: null,
            Filter: filter
        );

        var body = JsonSerializer.Serialize(req);
        var resp = await http.PostAsync($"/collections/{Collection}/points/search",
            new StringContent(body, Encoding.UTF8, "application/json"));

        if (!resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"Search failed: {resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
            return;
        }

        var resultJson = await resp.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<SearchResultWrapper>(resultJson);
        if (result?.Result == null || result.Result.Length == 0)
        {
            Console.WriteLine("No matches.");
            return;
        }

        // Persist search session and results to metastore (Postgres)
        var sessionId = await PersistSearchSessionAsync(
            queryText: queryText,
            vectorModel: Model,
            vectorDim: queryVector.Length,
            collectionName: Collection,
            limitRequested: req.Limit,
            scoreThreshold: req.ScoreThreshold,
            filter: filter,
            qdrantResults: result.Result
        );

        // Output the session id for reuse in MVC app
        Console.WriteLine($"SESSION_ID: {sessionId}");

        // Also print brief top results for immediate visibility
        foreach (var r in result.Result.Take(10))
        {
            var path = r.Payload != null && r.Payload.TryGetValue("path", out var pEl) &&
                       pEl.ValueKind == JsonValueKind.String
                ? pEl.GetString()
                : "(no path)";
            Console.WriteLine($"{r.Score:F4}  {path}");
        }
    }

    private static Filter? BuildFilter(string[] args)
    {
        if (args.Length == 0) return null;

        var must = new List<Condition>();

        string? GetArg(string key)
            => args.FirstOrDefault(a => a.StartsWith($"--{key}=", StringComparison.OrdinalIgnoreCase))?
                .Split('=', 2)[1];

        // Note: tenant/visibility/album/nsfw filters were removed as they are not defined in payload

        // New specific fields requested
        var eventName = GetArg("eventName");
        if (!string.IsNullOrWhiteSpace(eventName))
            must.Add(new Condition("eventName", new Match(Value: eventName, Any: null, Text: null), null));

        var yearName = GetArg("yearName");
        if (!string.IsNullOrWhiteSpace(yearName))
            must.Add(new Condition("yearName", new Match(Value: yearName, Any: null, Text: null), null));

        // Commerce rate (flattened from commerceData.rate)
        var commerceRateEq = GetArg("commerceRate");
        if (!string.IsNullOrWhiteSpace(commerceRateEq) && int.TryParse(commerceRateEq, out var crEq))
            must.Add(new Condition("commerceRate", new Match(Value: crEq, Any: null, Text: null), null));

        var commerceRateGte = GetArg("commerceRateGte");
        var commerceRateLte = GetArg("commerceRateLte");
        double? crGte = null, crLte = null;
        if (!string.IsNullOrWhiteSpace(commerceRateGte) && double.TryParse(commerceRateGte, out var g)) crGte = g;
        if (!string.IsNullOrWhiteSpace(commerceRateLte) && double.TryParse(commerceRateLte, out var l)) crLte = l;
        if (crGte.HasValue || crLte.HasValue)
            must.Add(new Condition("commerceRate", null, new Range(Gt: null, Gte: crGte, Lt: null, Lte: crLte)));

        // Tags: comma-separated, uses match.any against array payload
        var tagsStr = GetArg("tags");
        if (!string.IsNullOrWhiteSpace(tagsStr))
        {
            var tags = tagsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tags.Length > 0)
                must.Add(new Condition("tags", new Match(Value: null, Any: tags.Cast<object>().ToArray(), Text: null),
                    null));
        } 
        
        // Persons: comma-separated, uses match.any against array payload
        var personsStr = GetArg("persons");
        if (!string.IsNullOrWhiteSpace(personsStr))
        {
            var persons = personsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (persons.Length > 0)
                must.Add(new Condition("persons", new Match(Value: null, Any: persons.Cast<object>().ToArray(), Text: null),
                    null));
        }

        // Full-text filter on "text" field (requires text index in Qdrant)
        var textQuery = GetArg("text");
        if (!string.IsNullOrWhiteSpace(textQuery))
            must.Add(new Condition("text", new Match(Value: null, Any: null, Text: textQuery), null));

        if (must.Count == 0) return null;
        return new Filter(Must: must.ToArray(), Should: null, MustNot: null);
    }

    private async Task<string> GetOpenAiEmbedding(string input)
    {
        // 1) Try to get cached embedding response from Postgres
        var inputHash = input.AsSha256();
        await using (var conn = new NpgsqlConnection(_pgConnectionString))
        {
            await conn.OpenAsync();

            // Lookup cache
            const string selectSql =
                "SELECT response_json::text FROM embedding_cache " +
                "WHERE model = @model AND input_hash = @hash AND (expires_at IS NULL OR expires_at > now()) LIMIT 1";
            await using (var selectCmd = new NpgsqlCommand(selectSql, conn))
            {
                selectCmd.Parameters.AddWithValue("@model", NpgsqlDbType.Text, Model);
                selectCmd.Parameters.AddWithValue("@hash", NpgsqlDbType.Text, inputHash);
                await using var reader = await selectCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var cached = reader.GetString(0);
                    return cached;
                }
            }
        }

        // 2) Cache miss: request OpenAI
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiKey}");

        var requestBody = new
        {
            input,
            model = Model,
            encoding_format = "float"
        };

        var jsonBody = JsonSerializer.Serialize(requestBody);
        using var data = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(Url, data);
        var responseText = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error: {response.StatusCode}, Response: {responseText}");
        }

        // 3) Store in cache (best-effort)
        try
        {
            await using var conn = new NpgsqlConnection(_pgConnectionString);
            await conn.OpenAsync();
            const string upsertSql = @"INSERT INTO embedding_cache (model, input_hash, response_json, expires_at)
                                       VALUES (@model, @hash, @json, NULL)
                                       ON CONFLICT (model, input_hash)
                                       DO UPDATE SET response_json = EXCLUDED.response_json, created_at = now(), expires_at = EXCLUDED.expires_at";
            await using var upsertCmd = new NpgsqlCommand(upsertSql, conn);
            upsertCmd.Parameters.AddWithValue("@model", NpgsqlDbType.Text, Model);
            upsertCmd.Parameters.AddWithValue("@hash", NpgsqlDbType.Text, inputHash);
            upsertCmd.Parameters.AddWithValue("@json", NpgsqlDbType.Jsonb, responseText);
            await upsertCmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: failed to cache embedding: {ex.Message}");
        }

        return responseText;
    }

    private async Task<Guid> PersistSearchSessionAsync(
        string queryText,
        string vectorModel,
        int vectorDim,
        string collectionName,
        int limitRequested,
        float? scoreThreshold,
        Filter? filter,
        SearchResultItem[] qdrantResults)
    {
        var sessionId = Guid.NewGuid();

        // Serialize filter to JSON (or empty object)
        string filterJson = JsonSerializer.Serialize(filter ?? new Filter(null, null, null));

        await using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // Insert session row
        var insertSession = new NpgsqlCommand(@"INSERT INTO search_session
            (id, query_text, embedding_model, embedding_dim, embedding_hash, filter_json, collection_name, limit_requested, score_threshold, result_count)
            VALUES (@id, @query_text, @embedding_model, @embedding_dim, @embedding_hash, @filter_json, @collection_name, @limit_requested, @score_threshold, @result_count)",
            conn, tx);
        insertSession.Parameters.AddWithValue("@id", NpgsqlDbType.Uuid, sessionId);
        insertSession.Parameters.AddWithValue("@query_text", NpgsqlDbType.Text, queryText);
        insertSession.Parameters.AddWithValue("@embedding_model", NpgsqlDbType.Text, vectorModel);
        insertSession.Parameters.AddWithValue("@embedding_dim", NpgsqlDbType.Integer, vectorDim);
        insertSession.Parameters.AddWithValue("@embedding_hash", NpgsqlDbType.Text, (object?)DBNull.Value);
        insertSession.Parameters.AddWithValue("@filter_json", NpgsqlDbType.Jsonb, filterJson);
        insertSession.Parameters.AddWithValue("@collection_name", NpgsqlDbType.Text, collectionName);
        insertSession.Parameters.AddWithValue("@limit_requested", NpgsqlDbType.Integer, limitRequested);
        if (scoreThreshold.HasValue)
            insertSession.Parameters.AddWithValue("@score_threshold", NpgsqlDbType.Real, scoreThreshold.Value);
        else
            insertSession.Parameters.AddWithValue("@score_threshold", NpgsqlDbType.Real, (object)DBNull.Value);
        insertSession.Parameters.AddWithValue("@result_count", NpgsqlDbType.Integer, qdrantResults.Length);
        await insertSession.ExecuteNonQueryAsync();

        // Batch insert results
        if (qdrantResults.Length > 0)
        {
            var sb = new StringBuilder();
            sb.Append(
                "INSERT INTO search_session_result (session_id, rank, point_id, score, path_md5, payload_snapshot) VALUES ");
            var cmd = new NpgsqlCommand { Connection = conn, Transaction = tx };

            for (int i = 0; i < qdrantResults.Length; i++)
            {
                var r = qdrantResults[i];
                if (i > 0) sb.Append(",");

                string pointIdStr = r.Id.ValueKind switch
                {
                    JsonValueKind.String => r.Id.GetString()!,
                    JsonValueKind.Number => r.Id.GetRawText(),
                    _ => r.Id.GetRawText()
                };

                string? pathMd5 = null;
                if (r.Payload != null && r.Payload.TryGetValue("path", out var p) &&
                    p.ValueKind == JsonValueKind.String)
                    pathMd5 = p.GetString();

                // Build a compact payload snapshot with selected fields (safe for paging/render)
                Dictionary<string, object?>? snapshot = null;
                if (r.Payload != null)
                {
                    snapshot = new Dictionary<string, object?>();
                    if (pathMd5 != null) snapshot["path"] = pathMd5;
                    AddIfExists(r.Payload, snapshot, "eventName");
                    AddIfExists(r.Payload, snapshot, "yearName");
                    AddIfExists(r.Payload, snapshot, "commerceRate");
                    if (r.Payload.TryGetValue("eng30TagsData", out var tagsEl))
                        snapshot["eng30TagsData"] = tagsEl.ValueKind == JsonValueKind.Array
                            ? JsonSerializer.Deserialize<object>(tagsEl.GetRawText())
                            : null;
                }

                var payloadJson = snapshot != null ? JsonSerializer.Serialize(snapshot) : null;

                sb.Append($"(@sid, @rank_{i}, @pid_{i}, @score_{i}, @path_{i}, @ps_{i})");

                cmd.Parameters.AddWithValue("@rank_" + i, NpgsqlDbType.Integer, i);
                cmd.Parameters.AddWithValue("@pid_" + i, NpgsqlDbType.Text, pointIdStr);
                cmd.Parameters.AddWithValue("@score_" + i, NpgsqlDbType.Real, r.Score);
                if (pathMd5 != null)
                    cmd.Parameters.AddWithValue("@path_" + i, NpgsqlDbType.Text, pathMd5);
                else
                    cmd.Parameters.AddWithValue("@path_" + i, NpgsqlDbType.Text, (object)DBNull.Value);

                if (payloadJson != null)
                    cmd.Parameters.AddWithValue("@ps_" + i, NpgsqlDbType.Jsonb, payloadJson);
                else
                    cmd.Parameters.AddWithValue("@ps_" + i, NpgsqlDbType.Jsonb, (object)DBNull.Value);
            }

            cmd.Parameters.AddWithValue("@sid", NpgsqlDbType.Uuid, sessionId);
            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return sessionId;
    }

    private static void AddIfExists(Dictionary<string, JsonElement> payload, Dictionary<string, object?> snapshot,
        string key)
    {
        if (payload.TryGetValue(key, out var el))
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.String:
                    snapshot[key] = el.GetString();
                    break;
                case JsonValueKind.Number:
                    if (el.TryGetInt64(out var l)) snapshot[key] = l;
                    else if (el.TryGetDouble(out var d)) snapshot[key] = d;
                    else snapshot[key] = JsonSerializer.Deserialize<object>(el.GetRawText());
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    snapshot[key] = el.GetBoolean();
                    break;
                default:
                    snapshot[key] = JsonSerializer.Deserialize<object>(el.GetRawText());
                    break;
            }
        }
    }
}

record SearchRequest(
    [property: JsonPropertyName("vector")] float[] Vector,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("with_payload")]
    bool WithPayload,
    [property: JsonPropertyName("score_threshold")]
    float? ScoreThreshold,
    [property: JsonPropertyName("filter")] Filter? Filter
);

public record Filter(
    [property: JsonPropertyName("must")] Condition[]? Must,
    [property: JsonPropertyName("should")] Condition[]? Should,
    [property: JsonPropertyName("must_not")]
    Condition[]? MustNot
);

public record Condition(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("match")] Match? Match,
    [property: JsonPropertyName("range")] Range? Range
);

public record Match(
    [property: JsonPropertyName("value")] object? Value,
    [property: JsonPropertyName("any")] object[]? Any,
    [property: JsonPropertyName("text")] string? Text
);

public record Range(
    [property: JsonPropertyName("gt")] double? Gt,
    [property: JsonPropertyName("gte")] double? Gte,
    [property: JsonPropertyName("lt")] double? Lt,
    [property: JsonPropertyName("lte")] double? Lte
);

record SearchResultWrapper(
    [property: JsonPropertyName("result")] SearchResultItem[] Result
);

record SearchResultItem(
    [property: JsonPropertyName("id")] JsonElement Id,
    [property: JsonPropertyName("score")] float Score,
    [property: JsonPropertyName("payload")]
    Dictionary<string, JsonElement>? Payload
);