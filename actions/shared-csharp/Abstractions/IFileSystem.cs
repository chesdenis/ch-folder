namespace shared_csharp.Abstractions;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    void MoveFile(string sourceFileName, string destFileName);
}