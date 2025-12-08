using System.Text.RegularExpressions;
using shared_csharp;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace md5_image_hasher.Services;

public class FileNameMd5Processor
{
    private readonly IFileSystem _fileSystem;
    private readonly IFileHasher _fileHasher;

    public FileNameMd5Processor(IFileSystem fileSystem, IFileHasher fileHasher)
    {
        _fileSystem = fileSystem;
        _fileHasher = fileHasher;
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

        try
        {
            // Extract file information
            var directory = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");

            // Check and process file names based on the given convention
            var updatedFileName = await UpsertPrefixIntoFileName(filePath);
            var newFilePath = Path.Combine(directory, updatedFileName);

            // Rename the file if there is an update
            if (newFilePath != filePath)
            {
                _fileSystem.MoveFile(filePath, newFilePath);
                Console.WriteLine($"Renamed: {filePath} -> {newFilePath}");
            }
            else
            {
                Console.WriteLine($"No changes made to: {filePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file '{filePath}': {ex.Message}");
        }
    }

    private async Task<string> UpsertPrefixIntoFileName(string filePath)
    {
        if (Path.GetFileNameWithoutExtension(filePath).IsMd5InFileName())
        {
            // do nothing, the file name already has the md5 hash
            return filePath;
        }

        // Generate a new prefix that adheres to the convention
        var fileNameWithMd5 = await CalculateFileNameWithMd5Mark(filePath);
        return Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty,
            $"{fileNameWithMd5}{Path.GetExtension(filePath)}");
    }

    private async Task<string> CalculateFileNameWithMd5Mark(string existingFilePath)
    {
        // Split the file name into parts based on underscores
        var parts = Path.GetFileNameWithoutExtension(existingFilePath).Split('_');
        string part1 = parts.Length > 0 ? parts[0] : string.Empty;
        string part2 = parts.Length > 1 ? parts[1] : string.Empty;
        string part3 = parts.Length > 2 ? parts[2] : string.Empty;

        // Generate MD5 hash for the given file name
        string hash = await _fileHasher.ComputeMd5Async(existingFilePath);

        // Return the formatted prefix
        var partsOutput = new[] { part1, part2, part3, hash }.Where(x => !string.IsNullOrWhiteSpace(x));

        return string.Join("_", partsOutput);
    }
}