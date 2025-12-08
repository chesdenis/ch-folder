using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace shared_csharp.Infrastructure;

public class FileHasher : IFileHasher
{
    public Task<string> ComputeMd5Async(string filePath) => filePath.CalculateMd5Async();
}