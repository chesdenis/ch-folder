using Microsoft.Extensions.DependencyInjection;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;

namespace ai_content_query_builder;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IFileHasher, FileHasher>();
        services.AddSingleton<ContentQueryBuilder>();

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<ContentQueryBuilder>();

        await processor.RunAsync(args);
    }
}