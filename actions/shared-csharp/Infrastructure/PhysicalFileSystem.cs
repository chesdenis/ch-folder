using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace shared_csharp.Infrastructure;

public class PhysicalFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool DirectoryIsEmpty(string path) => !EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).Any();
    public bool FileExists(string path) => File.Exists(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);
    
    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) =>
        Directory.EnumerateDirectories(path, searchPattern, searchOption);

    public void MoveFile(string sourceFileName, string destFileName) => File.Move(sourceFileName, destFileName);
    
    public async Task<string> GetEmbAnswer(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEmbAnswer(filePath));
    public async Task<string> GetEmbConversation(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEmbConversation(filePath));
    public async Task<string> GetDqQuestion(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveDqQuestionPath(filePath));
    public async Task<string> GetCommerceMarkQuestion(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveCommerceMarkQuestionPath(filePath));
    public async Task<string> GetEng30TagsQuestion(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEng30TagsQuestionPath(filePath));
    public async Task<string> GetEngShortQuestion(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEngShortQuestionPath(filePath));
    public async Task<string> GetDqAnswer(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveDqAnswerPath(filePath));
    public async Task<string> GetCommerceMarkAnswer(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveCommerceMarkAnswerPath(filePath));
    public async Task<string> GetEng30TagsAnswer(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEng30TagsAnswerPath(filePath));
    public async Task<string> GetEngShortAnswer(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEngShortAnswerPath(filePath));
    public async Task<string> GetDqConversation(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveDqConversationPath(filePath));
    public async Task<string> GetCommerceMarkConversation(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveCommerceMarkConversationPath(filePath));
    public async Task<string> GetEng30TagsConversation(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEng30TagsConversationPath(filePath));
    public async Task<string> GetEngShortConversation(string filePath) => await File.ReadAllTextAsync(PathExtensions.ResolveEngShortConversationPath(filePath));
}