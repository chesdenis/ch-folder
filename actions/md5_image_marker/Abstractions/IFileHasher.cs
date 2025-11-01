namespace md5_image_hasher.Abstractions;

public interface IFileHasher
{
    Task<string> ComputeMd5Async(string filePath);
}