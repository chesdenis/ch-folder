using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace meta_uploader.Services;

public class MetaUploaderProcessor
{
    private readonly IFileSystem _fileSystem;

    public MetaUploaderProcessor(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
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
            if (_fileSystem.DirectoryExists(arg))
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

        try
        {

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file '{filePath}': {ex.Message}");
        }
    }
}