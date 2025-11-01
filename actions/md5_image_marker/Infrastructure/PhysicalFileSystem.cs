using md5_image_hasher.Abstractions;

namespace md5_image_hasher.Infrastructure;

public class PhysicalFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);

    public void MoveFile(string sourceFileName, string destFileName) => File.Move(sourceFileName, destFileName);
}