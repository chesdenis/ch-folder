namespace shared_csharp.Abstractions;

public interface IFileHasher
{
    Task<string> ComputeMd5Async(string filePath);
    Task<string> ComputeMd5ForceAsync(string filePath);
}