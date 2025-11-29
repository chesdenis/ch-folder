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

    public static async Task WalkThrough(this IFileSystem fileSystem, string[] args, Func<string, Task> processPath)
    {
        foreach (var arg in args)
        {
            if (fileSystem.DirectoryExists(arg))
            {
                foreach (var filePath in fileSystem.EnumerateFiles(arg, "*", SearchOption.TopDirectoryOnly))
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
}