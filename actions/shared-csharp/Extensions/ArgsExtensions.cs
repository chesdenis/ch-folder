using shared_csharp.Abstractions;

namespace shared_csharp.Extensions;

public static class ArgsExtensions
{
    public static string[] ValidateArgs(this string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide file paths as arguments.");
            var path = Console.ReadLine() ?? throw new Exception("Invalid file path.");
            path = path.Trim('\'', '\"');
            args = args.Append(path).ToArray();
        }
        
        return args;
    }

    public static async Task WalkThrough(this IFileSystem fileSystem, string[] args, Func<string, Task> processPath, bool recursive = false)
    {
        foreach (var arg in args)
        {
            if (fileSystem.DirectoryExists(arg))
            {
                // evaluate query result to avoid processing files again during async run
                var so = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var filesToProcess = fileSystem.EnumerateFiles(arg, "*", so).ToArray();
                    
                foreach (var filePath in filesToProcess)
                {
                    await processPath(filePath);
                }
            }
            else
            {
                var filePath = arg;
                await processPath(filePath);
            }
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