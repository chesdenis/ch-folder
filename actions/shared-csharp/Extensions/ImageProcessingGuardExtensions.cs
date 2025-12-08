namespace shared_csharp.Extensions;

public static class ImageProcessingGuardExtensions
{
    public static bool AllowImageToProcess(this string filePath)
    {
        if (string.Equals(Path.GetExtension(filePath), ".DS_Store", StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }

        if (string.Equals(Path.GetExtension(filePath), ".mov", StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }

        return true;
    }
}