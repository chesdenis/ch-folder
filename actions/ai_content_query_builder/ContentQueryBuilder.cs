using System.Text;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace ai_content_query_builder;

public class ContentQueryBuilder
{
    private readonly IFileSystem _fileSystem;

    public ContentQueryBuilder(IFileSystem fileSystem)
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
    
    private static async Task ProcessSingleFile(string filePath)
    {
        if (!filePath.AllowImageToProcess())
        {
            return;
        }
        
        try
        {
            var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
            if (groupName.Length != 4)
            {
                groupName = Path.GetFileNameWithoutExtension(filePath);
            }
            // Extract file information
            var directory = Path.GetDirectoryName(filePath) ?? throw new Exception("Invalid file path.");
            var previewFolder = Path.Combine(directory, "preview");
            var previewFileName = Path.GetFileNameWithoutExtension(filePath) + "_p2000.jpg";
            
            var previewPath = Path.Combine(previewFolder, previewFileName);
            
            await GenerateDescriptionQuery(previewPath, directory, groupName);
            await GenerateEnglish10Words(previewPath, directory, groupName);
            await GenerateTags(previewPath, directory, groupName);
            await GenerateCommerceMark(previewPath, directory, groupName);

            Console.WriteLine($"File {filePath} processed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file '{filePath}': {ex.Message}");
        }
    }

    private static async Task GenerateDescriptionQuery(string previewPath, string directory, string groupName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Give me detailed description of this picture using russian language @\"{previewPath}\"");
        Directory.CreateDirectory(Path.Combine(directory, "dq"));
        await File.WriteAllTextAsync(Path.Combine(directory,"dq", $"{groupName}.dq.md"), sb.ToString());
    }
    
    private static async Task GenerateEnglish10Words(string previewPath, string directory, string groupName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Give me short english description of this picture @\"{previewPath}\". Description must be from 7 to 10 words");
        Directory.CreateDirectory(Path.Combine(directory, "engShort"));
        await File.WriteAllTextAsync(Path.Combine(directory,"engShort", $"{groupName}.engShort.md"), sb.ToString());
    } 
    
    private static async Task GenerateTags(string previewPath, string directory, string groupName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Give me english tags of this picture @\"{previewPath}\". Tags count must be less than 30 items. Order them by relevance from most to less suitable");
        Directory.CreateDirectory(Path.Combine(directory, "eng30tags"));
        await File.WriteAllTextAsync(Path.Combine(directory,"eng30tags", $"{groupName}.eng30tags.md"), sb.ToString());
    } 
    
    private static async Task GenerateCommerceMark(string previewPath, string directory, string groupName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Give me commercial image stock potential of this picture @\"{previewPath}\". " +
                      $"Rate this from 1 to 5 where 1 is lowest and 5 is highest. " +
                      $"Format answer as json complex object with fields: rate [number], rate-explanation [string]");
        Directory.CreateDirectory(Path.Combine(directory, "commerceMark"));
        await File.WriteAllTextAsync(Path.Combine(directory,"commerceMark", $"{groupName}.commerceMark.md"), sb.ToString());
    }
}