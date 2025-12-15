using System.Text;
using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace ai_content_answer_builder;

public class AiContentAnswerBuilder
{
    private readonly IFileSystem _fileSystem;
    
    public AiContentAnswerBuilder(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }
    
    public async Task RunAsync(string[] args)
    {
        args = args.ValidateArgs();
        await _fileSystem.WalkThrough(args, ProcessSingleFile);
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
            
            await BuildDescription(previewPath, directory, groupName);
            await BuildEnglish10Words(previewPath, directory, groupName);
            await BuildTags(previewPath, directory, groupName);
            await BuildCommerceMark(previewPath, directory, groupName);

            Console.WriteLine($"File {filePath} processed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file '{filePath}': {ex.Message}");
        }
    }

    private static async Task BuildDescription(string previewPath, string directory, string groupName)
    {
        var expectedPath = Path.Combine(directory, "dq", $"{groupName}.dq.md");
        
    }
    
    private static async Task BuildEnglish10Words(string previewPath, string directory, string groupName)
    {
        var expectedPath = Path.Combine(directory, "engShort", $"{groupName}.engShort.md");
    } 
    
    private static async Task BuildTags(string previewPath, string directory, string groupName)
    {
        var expectedPath = Path.Combine(directory, "eng30tags", $"{groupName}.eng30tags.md");
    } 
    
    private static async Task BuildCommerceMark(string previewPath, string directory, string groupName)
    {
        var expectedPath = Path.Combine(directory, "commerceMark", $"{groupName}.commerceMark.md");
        
    }
}