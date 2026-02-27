namespace shared_csharp.Abstractions;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);
    Task<FileMetadata?> GetMetadata(string filePath);
    Task<string> GetGroup(string filePath);
    Task<string> GetEmbAnswer(string filePath);
    Task<string> GetDqAnswer(string filePath);
    Task<string> GetCommerceMarkAnswer(string filePath);
    Task<string> GetEngShortAnswer(string filePath);
    Task<string[]> GetEng30Tags(string filePath);
    Task<CommerceJson?> GetCommerceMarkAnswerJson(string filePath);
}