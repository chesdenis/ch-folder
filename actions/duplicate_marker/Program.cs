using duplicate_marker;
using Microsoft.Extensions.DependencyInjection;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;

var services = new ServiceCollection();

services.AddSingleton<IFileSystem, PhysicalFileSystem>();
services.AddSingleton<IFileHasher, FileHasher>();
services.AddSingleton<DuplicateMarker>();

var provider = services.BuildServiceProvider();

await provider.GetRequiredService<DuplicateMarker>().RunAsync(args);


