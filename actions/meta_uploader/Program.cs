using meta_uploader;
using Microsoft.Extensions.DependencyInjection;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;

var services = new ServiceCollection();

services.AddSingleton<IFileSystem, PhysicalFileSystem>();
services.AddSingleton<IFileHasher, FileHasher>();
services.AddSingleton<MetaUploader>();
services.AddSingleton<EmbeddingUploader>();

var provider = services.BuildServiceProvider();

await provider.GetRequiredService<MetaUploader>().RunAsync(args);
await provider.GetRequiredService<EmbeddingUploader>().RunAsync(args);
