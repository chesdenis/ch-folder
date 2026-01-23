using Microsoft.Extensions.DependencyInjection;
using mirror_service.Services;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;

var services = new ServiceCollection();

services.AddSingleton<IFileSystem, PhysicalFileSystem>();
services.AddSingleton<IFileHasher, FileHasher>();
services.AddSingleton<BackupProcessor>();

var provider = services.BuildServiceProvider();
var processor = provider.GetRequiredService<BackupProcessor>();

await processor.RunAsync(args);