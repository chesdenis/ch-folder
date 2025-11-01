using md5_image_hasher.Abstractions;
using shared_csharp;

namespace md5_image_hasher.Infrastructure;

public class FileHasher : IFileHasher
{
    public Task<string> ComputeMd5Async(string filePath)
    {
        // Delegates to the shared extension that reads the file in production.
        return filePath.CalculateMd5Async();
    }
}