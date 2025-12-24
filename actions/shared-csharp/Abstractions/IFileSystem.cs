namespace shared_csharp.Abstractions;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    bool DirectoryIsEmpty(string path);
    bool FileExists(string path);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);
    void MoveFile(string sourceFileName, string destFileName);
    Task<string> GetEmbAnswer(string filePath);
    Task<string> GetEmbConversation(string filePath);
    Task<string> GetDqQuestion(string filePath);
    Task<string> GetCommerceMarkQuestion(string filePath);
    Task<string> GetEng30TagsQuestion(string filePath);
    Task<string> GetEngShortQuestion(string filePath);
    Task<string> GetDqAnswer(string filePath);
    Task<string> GetCommerceMarkAnswer(string filePath);
    Task<string> GetEng30TagsAnswer(string filePath);
    Task<string> GetEngShortAnswer(string filePath);
    Task<string> GetDqConversation(string filePath);
    Task<string> GetCommerceMarkConversation(string filePath);
    Task<string> GetEng30TagsConversation(string filePath);
    Task<string> GetEngShortConversation(string filePath);
}