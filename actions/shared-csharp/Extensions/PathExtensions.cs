namespace shared_csharp.Extensions;

public static class PathExtensions
{
    public static string GetGroupName(this string filePath)
    {
        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }
        
        return groupName;
    }
    
    public static object[][] CollectStorageFolders (string contextPath)
    {
        if (!Directory.Exists(contextPath))
            return Array.Empty<object[]>();

        var result = new List<object[]>();

        // Level 1: immediate subfolders of contextPath
        var firstLevelDirs = Directory.GetDirectories(contextPath, "*", SearchOption.TopDirectoryOnly);
        foreach (var dr in firstLevelDirs)
        {
            var directoryName = Path.GetFileName(dr);
            if (string.IsNullOrWhiteSpace(directoryName) || directoryName.StartsWith("."))
                continue;

            // include the level-1 folder itself
            result.Add([directoryName]);

            // Level 2: subfolders of each level-1 folder
            var level2Dirs = Directory.GetDirectories(dr, "*", SearchOption.TopDirectoryOnly);
            foreach (var dir2 in level2Dirs)
            {
                var name2 = Path.GetFileName(dir2);
                if (string.IsNullOrWhiteSpace(name2) || name2.StartsWith("."))
                    continue;

                var relative = Path.Combine(directoryName, name2);
                result.Add([relative]);
            }
        }

        return result.ToArray();
    }
    
    public static IEnumerable<object[]> GetFilesInFolder(string contextPath, object[][] storageFolders, Action<string> onFolderProcessed = null)
    {
        foreach (var inputArgs in storageFolders)
        foreach (string arg in inputArgs)
        {
            var folderPath = Path.Combine(contextPath, arg);
            // Ensure the folder exists
            if (!Directory.Exists(folderPath))
                continue;

            // Get all files in the folder and add their names to the data
            // use top directory only because other folders are system, preview, etc.
            var files = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly).Select(s => new
            {
                fileName = Path.GetFileName(s),
                filePath = s
            }).ToArray();
            // excluding preview and system files and unsupported file types
            files = files.Where(f => !f.fileName.EndsWith(".DS_Store")).ToArray();
            files = files.Where(f => !f.fileName.EndsWith(".mov")).ToArray();
            files = files.Where(f => !f.fileName.EndsWith(".MOV")).ToArray();
            files = files.Where(f => !f.fileName.EndsWith(".mp4")).ToArray();
            files = files.Where(f => !f.fileName.EndsWith(".MP4")).ToArray();
            files = files.Where(f => !f.fileName.StartsWith("._")).ToArray();
            foreach (var file in files)
            {
                yield return [file.filePath];
            }
            
            onFolderProcessed?.Invoke(folderPath);
        }
    }
}