using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace shared_csharp.Extensions;

public static class FileCalculationExtensions
{
    public static readonly Regex Md5PrefixRegex = new Regex(@"^[a-fA-F0-9]{32}$", RegexOptions.Compiled);

    public static async Task<string> CalculateMd5Async(this string filePath)
    {
        // check if file already has the md5 prefix,
        // this is because we want to avoid re-computing it
        if (filePath.IsMd5InFileName())
        {
            return filePath.GetMd5FromFileName();
        }
        
        using var md5 = MD5.Create();

        // Read the file in a memory-efficient asynchronous manner
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await md5.ComputeHashAsync(stream);

        // Convert byte array to hexadecimal string
        var sb = new StringBuilder();
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }
}