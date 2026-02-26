using Microsoft.Extensions.DependencyInjection;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;

namespace group_folder_extractor;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IFileHasher, FileHasher>();
        services.AddSingleton<GroupFolderExtractor>();

        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<GroupFolderExtractor>();

        await processor.RunAsync(args);
    }
}