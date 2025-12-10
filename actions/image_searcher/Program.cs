using Microsoft.Extensions.DependencyInjection;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;

var services = new ServiceCollection();

services.AddSingleton<IFileSystem, PhysicalFileSystem>();

var provider = services.BuildServiceProvider();