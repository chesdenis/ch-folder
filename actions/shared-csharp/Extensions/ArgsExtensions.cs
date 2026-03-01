using shared_csharp.Abstractions;

namespace shared_csharp.Extensions;

public static class ArgsExtensions
{
    public static string[] ValidateArgs(this string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please arguments for processing");
            var path = Console.ReadLine() ?? throw new Exception("Invalid file path.");
            path = path.Trim('\'', '\"');
            args = args.Append(path).ToArray();
        }
        
        return args;
    }

    public static async Task WalkThrough(this IContentProvider contentProvider, string[] args, Func<string, Task> processPath)
    {
        foreach (var arg in args)
        {
            var md5List = await contentProvider.GetFilePointers(Convert.ToUInt32(arg));
            Console.WriteLine($"Found {md5List.Length} items to process");
            foreach (var md5 in md5List)
            {
                await processPath(md5);
            }
            Console.WriteLine($"Done {md5List.Length} items");
        }
    }
    
    public static string AsBase64String(this string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes);
    }

    public static T ThisJsonAs<T>(this string jsonData) where T : class
    {
        if (string.IsNullOrWhiteSpace(jsonData)) return null!;

        return System.Text.Json.JsonSerializer.Deserialize<T>(jsonData, 
            new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }
}