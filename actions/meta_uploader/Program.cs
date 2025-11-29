using meta_uploader.Services;
using Microsoft.Extensions.DependencyInjection;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;

var services = new ServiceCollection();

services.AddSingleton<IFileSystem, PhysicalFileSystem>();
services.AddSingleton<IFileHasher, FileHasher>();
services.AddSingleton<MetaUploader>();

var provider = services.BuildServiceProvider();

var processor = provider.GetRequiredService<MetaUploader>();
await processor.RunAsync(args);
