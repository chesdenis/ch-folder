namespace shared_csharp.Extensions;

public static class ImageProcessingGuardExtensions
{
    public static bool AllowImageToProcess(this string filePath)
    {
        if (string.Equals(Path.GetExtension(filePath), ".DS_Store", StringComparison.InvariantCultureIgnoreCase))
        {
            Console.WriteLine($"Skipping .DS_Store file: {filePath}");
            return false;
        }

        if (string.Equals(Path.GetExtension(filePath), ".mov", StringComparison.InvariantCultureIgnoreCase))
        {
            Console.WriteLine($"Skipping .mov file: {filePath}");
            return false;
        }

        return true;
    }
}