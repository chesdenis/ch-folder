namespace shared_csharp.Extensions;

public static class PathExtensions
{
    public static string GetGroupName(this string filePath)
    {
        var groupName = Path.GetFileNameWithoutExtension(filePath).Split("_")[0];
        if (groupName.Length != 4)
        {
            groupName = Path.GetFileNameWithoutExtension(filePath);
        }
        
        return groupName;
    }
}