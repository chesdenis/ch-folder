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
                // evaluate query result to avoid processing files again during async run
                var filesToProcess = fileSystem.EnumerateFiles(arg, "*", SearchOption.TopDirectoryOnly).ToArray();
                    
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

    public static async Task WalkFolders(this IFileSystem fileSystem, string[] args, Func<string, Task> processPath)
    {
        foreach (var arg in args)
        {
            if (!fileSystem.DirectoryExists(arg)) continue;

            var filesToProcess = fileSystem.EnumerateDirectories(arg, "*", SearchOption.TopDirectoryOnly).ToArray();
            foreach (var filePath in filesToProcess)
            {
                await processPath(filePath);
            }
        }
    }
}