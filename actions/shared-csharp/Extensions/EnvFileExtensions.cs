namespace shared_csharp.Extensions;

public static class EnvFileExtensions
{
    public static string GetEnvVariableFromFile(string fileName, string variableName) =>
        File.ReadAllLines(fileName)
            .FirstOrDefault(line => line.StartsWith($"{variableName}=", StringComparison.OrdinalIgnoreCase))?
            .Split('=', 2)[1] ?? throw new Exception($"Could not find {variableName} in {fileName}.");
}