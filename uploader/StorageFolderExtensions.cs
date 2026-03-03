using shared_csharp.Extensions;

namespace uploader;

public static class StorageFolderExtensions
{
    /// <summary>
    /// Storage folders are 2 level deep. First level is yearly based partition, second level is event partition
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<string> GetStorageFolders(string rootStoragePath)
    {
        if (!Directory.Exists(rootStoragePath))
            yield break;
        
        // Level 1: immediate subfolders of contextPath
        var firstLevelDirs = Directory.GetDirectories(rootStoragePath, "*", SearchOption.TopDirectoryOnly);
        foreach (var dr in firstLevelDirs)
        {
            var directoryName = Path.GetFileName(dr);
            if (string.IsNullOrWhiteSpace(directoryName) || directoryName.StartsWith("."))
                continue;

            // include the level-1 folder itself
            yield return directoryName;

            // Level 2: subfolders of each level-1 folder
            var level2Dirs = Directory.GetDirectories(dr, "*", SearchOption.TopDirectoryOnly);
            foreach (var dir2 in level2Dirs)
            {
                var name2 = Path.GetFileName(dir2);
                if (string.IsNullOrWhiteSpace(name2) || name2.StartsWith("."))
                    continue;

                var relative = Path.Combine(directoryName, name2);
                
                yield return relative;
            }
        }
    }
    
    public static IEnumerable<string> GetFilesInFolder(string contextPath, IEnumerable<string> storageFolders, Action<string> onFolderProcessed = null)
    {
        foreach (var storageFolder in storageFolders)
        {
            var folderPath = Path.Combine(contextPath, storageFolder);
            // Ensure the folder exists
            if (!Directory.Exists(folderPath))
                continue;

            // Get all files in the folder and add their names to the data
            // use top directory only because other folders are system, preview, etc.
            var files = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Select(s => new
            {
                fileName = Path.GetFileName(s),
                filePath = s
            });
            // excluding preview and system files and unsupported file types
            files = files.Where(w => !w.fileName.StartsWith("._")).ToArray();
            
            foreach (var file in files)
            {
                yield return file.filePath;
            }

            onFolderProcessed?.Invoke(folderPath);
        }
    }
}