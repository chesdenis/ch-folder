using meta_uploader;
using Microsoft.Extensions.DependencyInjection;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;

var services = new ServiceCollection();

services.AddSingleton<IFileSystem, PhysicalFileSystem>();
services.AddSingleton<IFileHasher, FileHasher>();
services.AddSingleton<ImageMetaUploader>();
services.AddSingleton<ImageEmbeddingUploader>();

var provider = services.BuildServiceProvider();

await provider.GetRequiredService<ImageMetaUploader>().RunAsync(args);
await provider.GetRequiredService<ImageEmbeddingUploader>().RunAsync(args);
