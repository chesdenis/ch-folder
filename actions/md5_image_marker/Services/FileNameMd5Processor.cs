using System.Text.RegularExpressions;
using md5_image_hasher.Abstractions;
using shared_csharp;

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
        if (IsMd5InFileName(Path.GetFileNameWithoutExtension(filePath)))
        {
            // do nothing, the file name already has the md5 hash
            return filePath;
        }

        // Generate a new prefix that adheres to the convention
        var fileNameWithMd5 = await CalculateFileNameWithMd5Mark(filePath);
        return Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty,
            $"{fileNameWithMd5}{Path.GetExtension(filePath)}");
    }

    private static bool IsMd5InFileName(string fileName)
    {
        // Get the file name without the extension
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // Split by underscores
        var parts = fileNameWithoutExtension.Split('_');

        // Check if the last part is a valid MD5 hash (32 hexadecimal characters)
        var md5Regex = new Regex("^[a-fA-F0-9]{32}$", RegexOptions.Compiled);

        return parts.Length > 0 && md5Regex.IsMatch(parts[^1]); // Check the last part
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