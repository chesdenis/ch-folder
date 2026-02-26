using Microsoft.Extensions.DependencyInjection;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;

namespace embedding_downloader;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IFileHasher, FileHasher>();
        services.AddSingleton<OpenAiEmbeddingDownloader>();

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<OpenAiEmbeddingDownloader>();

        await processor.RunAsync(args);
    }
}