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
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide file paths as arguments.");
            var path = Console.ReadLine() ?? throw new Exception("Invalid file path.");
            path = path.Trim('\'', '\"');
            args = args.Append(path).ToArray();
        }

        foreach (var arg in args)
        {
            if (_fileSystem.DirectoryExists(arg)) // if it is a directory, iterate over all files in it
            {
                foreach (var filePath in _fileSystem.EnumerateFiles(arg, "*", SearchOption.TopDirectoryOnly))
                {
                    await ProcessSingleFile(filePath);
                }
            }
            else
            {
                var filePath = arg;
                await ProcessSingleFile(filePath);
            }
        }
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