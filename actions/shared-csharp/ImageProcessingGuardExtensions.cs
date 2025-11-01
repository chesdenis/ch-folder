namespace shared_csharp;

public static class ImageProcessingGuardExtensions
{
    public static bool AllowImageToProcess(this string filePath)
    {
        // Validate if the file exists
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return false;
        }

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