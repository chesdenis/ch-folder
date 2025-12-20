using System.Text;
using Newtonsoft.Json;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace embedding_downloader;

public class OpenAiEmbeddingDownloader(IFileSystem fileSystem)
{
    private readonly string _apiKey = Environment.GetEnvironmentVariable("OpenAiApiKey")
                                      ?? throw new ArgumentNullException("OpenAiApiKey");
    private const string model = "text-embedding-ada-002";
    private const string endpoint = "https://api.openai.com/v1/embeddings";

    private static readonly List<string> filesToProcess = new List<string>();

    public async Task RunAsync(string[] args)
    {
        args = args.ValidateArgs();
        await fileSystem.WalkThrough(args, async s =>
        {
            filesToProcess.Add(s);
            await Task.CompletedTask;
        });
       
        
        Console.WriteLine($"Found {filesToProcess.Count} files to process.");

        int total = filesToProcess.Count;
        int processed = 0;
        
        await fileSystem.WalkThrough(args, async(p)=>
        {
            await ProcessSingleFile(p);
            Interlocked.Increment(ref processed);
            
            Console.WriteLine($"[{processed}/{total}] Downloaded embeddings for {p}");
        });
    }

    private async Task ProcessSingleFile(string filePath)
    {
        if (!filePath.AllowImageToProcess())
        {
            return;
        }

        try
        {
            // Extract description information
            var groupName = filePath.GetGroupName();
            var directoryName = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
            var descriptionFolder = Path.Combine(directoryName, "dq");
            var descriptionPath = Path.Combine(descriptionFolder, $"{groupName}.dq.md.answer.md");
            var descriptionText = await File.ReadAllTextAsync(descriptionPath);
            
            var embeddingFolder = Path.Combine(directoryName, "emb");
            Directory.CreateDirectory(embeddingFolder);

            var embeddingAnswerPath = Path.Combine(embeddingFolder, $"{groupName}.emb.md.answer.md");
            var embeddingConversationPath = Path.Combine(embeddingFolder, $"{groupName}.emb.md.conversation.md");

            if (File.Exists(embeddingAnswerPath))
            {
                Console.WriteLine($"File {filePath} skipped because computed already.");
                return;
            }
            
            // Call the OpenAI API
            var response = await GetOpenAiEmbedding(_apiKey, descriptionText);
            var sb = new StringBuilder();
            sb.AppendLine("### user");
            sb.AppendLine(descriptionText);
            sb.AppendLine("### assistant");
            sb.AppendLine(response);
            
            await File.WriteAllTextAsync(embeddingConversationPath, sb.ToString());
            await File.WriteAllTextAsync(embeddingAnswerPath, response);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
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