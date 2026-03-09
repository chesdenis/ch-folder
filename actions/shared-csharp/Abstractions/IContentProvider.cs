namespace shared_csharp.Abstractions;

public interface IContentProvider
{
    Task<string> GetPartition(string md5);
    Task<string> GetSection(string md5);
    Task<string> GetGroup(string md5);
    Task<string> GetAverageHash(string md5);
    Task<string> GetColorHash(string md5);
    Task<string> GetDescription(string md5);
    Task<string[]> GetTags(string md5);
    
    Task<string> GetEmbAnswer(string md5);
    Task<string> GetEmbConversation(string md5);
    
    Task<string> GetDqQuestion(string md5);
    Task<string> GetDqAnswer(string md5);
    Task<string> GetDqConversation(string md5);
    Task<string> GetCommerceMarkQuestion(string md5);
    Task<string> GetCommerceMarkAnswer(string md5);
    Task<string> GetCommerceMarkConversation(string md5);
    
    Task<string> GetEng30TagsQuestion(string md5);
    Task<string> GetEng30TagsAnswer(string md5);
    Task<string> GetEng30TagsConversation(string md5);
    Task<string[]> GetEng30Tags(string md5);
    
    Task<string> GetEngShortQuestion(string md5);
    Task<string> GetEngShortAnswer(string md5);
    Task<string> GetEngShortConversation(string md5);
    
    Task<string[]> GetFilePointers(uint partition);
    Task<FileMetadata?> GetMetadataByMd5(string md5);
   
    Task<CommerceJson?> GetCommerceMarkAnswerJson(string md5);
    Task<string> GetExtension(string md5);
    Task<FileMetadata?> GetMetadataWithPreviews(string md5);
    Task<byte[]> GetReal(string md5);
 
   
}