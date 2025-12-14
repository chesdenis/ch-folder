using shared_csharp.Abstractions;
using shared_csharp.Extensions;

namespace duplicate_marker;

public class DuplicateMarker(IFileSystem fileSystem, IFileHasher fileHasher)
{
    public async Task RunAsync(string[] args)
    {
        args = args.ValidateArgs();
        await fileSystem.WalkThrough(args, ProcessSingleFile);
    }

    private async Task ProcessSingleFile(string filePath)
    {
        if (!filePath.AllowImageToProcess())
        {
            return;
        }

        var md5 = await filePath.CalculateMd5Async();
    }
}