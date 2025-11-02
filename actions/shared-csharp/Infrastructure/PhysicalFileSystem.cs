using shared_csharp.Abstractions;

namespace shared_csharp.Infrastructure;

public class PhysicalFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);

    public void MoveFile(string sourceFileName, string destFileName) => File.Move(sourceFileName, destFileName);
}