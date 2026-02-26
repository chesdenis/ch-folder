using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace group_folder_extractor;

public class GroupFolderExtractor(IFileSystem fileSystem)
{
    public async Task RunAsync(string[] args)
    {
        args = args.ValidateArgs();
        
        await fileSystem.WalkFolders(args, ProcessFolder);
    }

    private Task ProcessFolder(string folderPath)
    {
        var folderName = Path.GetFileName(folderPath);
        if (string.IsNullOrWhiteSpace(folderName)) return Task.CompletedTask;

        // Only process folders with 4 alphabetic characters
        if (folderName.Length != 4 || !folderName.All(char.IsLetter))
            return Task.CompletedTask;

        var files = fileSystem.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly).ToList();
        if (files.Count == 0)
            return Task.CompletedTask;

        var requiredPrefix = folderName + "_";

        // 1) Ensure each file has the correct prefix
        foreach (var file in files)
        {
            try
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith(requiredPrefix)) continue;

                var renamedPath = Path.Combine(folderPath, requiredPrefix + name);
                fileSystem.MoveFile(file, renamedPath);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        // Refresh files list after potential renames
        files = fileSystem.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly).ToList();

        // 2) Move files to the parent directory
        var parent = Path.GetDirectoryName(folderPath);
        if (string.IsNullOrWhiteSpace(parent))
            return Task.CompletedTask;

        foreach (var file in files)
        {
            try
            {
                var target = Path.Combine(parent, Path.GetFileName(file));
                fileSystem.MoveFile(file, target);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        return Task.CompletedTask;
    }
}
 