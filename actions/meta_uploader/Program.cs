using meta_uploader;
using Microsoft.Extensions.DependencyInjection;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;

var services = new ServiceCollection();

services.AddSingleton<IContentProvider, RemoteContentProvider>(
    sp => new RemoteContentProvider(sp.GetService<IHttpClientFactory>(), 
        Environment.GetEnvironmentVariable("RCP_ENDPOINT")) );
services.AddSingleton<ImageMetaUploader>();
services.AddSingleton<FileMetaUploader>();
services.AddSingleton<ImageEmbeddingUploader>();
services.AddHttpClient();

var provider = services.BuildServiceProvider();

await provider.GetRequiredService<ImageMetaUploader>().RunAsync(args);
Console.WriteLine("Done image meta uploader");
await provider.GetRequiredService<FileMetaUploader>().RunAsync(args);
Console.WriteLine("Done file meta uploader");
await provider.GetRequiredService<ImageEmbeddingUploader>().RunAsync(args);
Console.WriteLine("Done embedding uploader");
