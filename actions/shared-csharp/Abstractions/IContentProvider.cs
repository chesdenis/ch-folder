namespace shared_csharp.Abstractions;

public interface IContentProvider
{
    Task<string[]> GetFilePointers(uint partition);
    Task<FileMetadata?> GetMetadataByMd5(string md5);
    Task<string> GetEmbAnswer(string md5);
    Task<string> GetDqAnswer(string md5);
    Task<string> GetCommerceMarkAnswer(string md5);
    Task<string> GetEngShortAnswer(string md5);
    Task<string[]> GetEng30Tags(string md5);
    Task<CommerceJson?> GetCommerceMarkAnswerJson(string md5);
    Task<string> GetExtension(string md5);
    Task<FileMetadata?> GetMetadataWithPreviews(string md5);
    Task<byte[]> GetReal(string md5);
    Task<string> GetSection(string md5);
    Task<string> GetPartition(string md5);
    Task<string> GetGroup(string md5);
}