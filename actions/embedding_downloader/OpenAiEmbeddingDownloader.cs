using System.Text;
using Newtonsoft.Json;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace embedding_downloader;

public class OpenAiEmbeddingDownloader
{
    private readonly IFileSystem _fileSystem;
    private readonly string _apiKey;
    private const string model = "text-embedding-ada-002";
    private const string endpoint = "https://api.openai.com/v1/embeddings";

    public OpenAiEmbeddingDownloader(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _apiKey = Environment.GetEnvironmentVariable("OpenAiApiKey")
                  ?? throw new ArgumentNullException("OpenAiApiKey");
    }

    public async Task RunAsync(string[] args)
    {
        args = args.ValidateArgs();
        await _fileSystem.WalkThrough(args, ProcessSingleFile);
    }

    private async Task ProcessSingleFile(string filePath)
    {
        if (!filePath.AllowImageToProcess())
        {
            return;
        }

        
        // Extract description information
        var groupName = filePath.GetGroupName();
        var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
        var descriptionFolder = Path.Combine(directoryName, "dq");
        var descriptionPath = Path.Combine(descriptionFolder, $"{groupName}.dq.md.answer.md");
        var descriptionText = await File.ReadAllTextAsync(descriptionPath);

        var embeddingLocation = Path.Combine(descriptionFolder, $"{groupName}.dq.emb.json");

        if (File.Exists(embeddingLocation))
        {
            Console.WriteLine($"File {filePath} skipped because computed already.");
            return;
        }

        string input = descriptionText;

        // Call the OpenAI API
        var response = await GetOpenAiEmbedding(_apiKey, input);
        
        await File.WriteAllTextAsync(embeddingLocation, response);
                
        Console.WriteLine($"File {filePath} processed successfully.");
    }

    private static async Task<string> GetOpenAiEmbedding(string apiKey, string input)
    {
        using var client = new HttpClient();

        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var jsonBody = JsonConvert.SerializeObject(new
        {
            input = input,
            model = model,
            encoding_format = "float"
        });
        
        var data = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(endpoint, data);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();
        }

        var errorResponse = await response.Content.ReadAsStringAsync();
        throw new Exception($"Error: {response.StatusCode}, Response: {errorResponse}");
    }
}