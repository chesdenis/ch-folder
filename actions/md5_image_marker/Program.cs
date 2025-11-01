using md5_image_hasher.Abstractions;
using md5_image_hasher.Infrastructure;
using md5_image_hasher.Services;
using Microsoft.Extensions.DependencyInjection;

namespace md5_image_hasher;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IFileHasher, FileHasher>();
        services.AddSingleton<FileNameMd5Processor>();

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<FileNameMd5Processor>();

        await processor.RunAsync(args);
    }
}