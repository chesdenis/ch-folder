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
    
    public static object[][] CollectStorageFolders (string contextPath) =>
        File.ReadAllLines(Path.Combine(contextPath, "spec.info"))
            .Select(line => line
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => (object)s.Trim())
                .ToArray())
            .Where(arr => arr.Length > 0)
            .ToArray();
    
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