using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Text.Json;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;
using shared_csharp.Extensions;

namespace content_validator;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: content_validator <folder> [--test-kind <name>] [--job-id <uuid>]");
                return 2;
            }

            var folder = args[0]; // container-mounted path (e.g., /in)
            string testKind = DecodeB64(GetArgValue(args, "--test-kind")) ?? "file_has_correct_md5_prefix";
            var folderName = DecodeB64(GetArgValue(args, "--folder-name"));
            if (string.IsNullOrWhiteSpace(folderName))
            {
                // fallback to the last segment of the container path or the path itself
                folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
                if (string.IsNullOrWhiteSpace(folderName)) folderName = folder;
            }

            // Generate internal job id per container run
            var jobGuid = Guid.NewGuid();
            var jobId = jobGuid.ToString("N");

            var services = new ServiceCollection();
            services.AddSingleton<IFileSystem, PhysicalFileSystem>();
            services.AddSingleton<IFileHasher, FileHasher>();
            await using var provider = services.BuildServiceProvider();

            var fs = provider.GetRequiredService<IFileSystem>();

            var connString = string.Join(";",
                $"Host={Environment.GetEnvironmentVariable("PG_HOST")}",
                $"Port={Environment.GetEnvironmentVariable("PG_PORT")}",
                $"Database={Environment.GetEnvironmentVariable("PG_DATABASE")}",
                $"Username={Environment.GetEnvironmentVariable("PG_USERNAME")}",
                $"Password={Environment.GetEnvironmentVariable("PG_PASSWORD")}",
                "Ssl Mode=Disable",
                "Trust Server Certificate=true",
                "Include Error Detail=true");

            Console.WriteLine(
                $"Job {jobId}: Content validation started for folder '{folderName}' with test '{testKind}'");

            await UpsertStatusAsync(connString, jobGuid, folderName, testKind, "Running", new { message = "started" },
                startedAtOnly: true);

            int exitCode = 0;
            object? finalDetails = null;
            try
            {
                switch (testKind)
                {
                    case "file_has_correct_md5_prefix":
                        var res = await RunFileHasCorrectMd5PrefixAsync(fs, folder,
                            async details => { Console.WriteLine(details.message); });
                        exitCode = res.ExitCode;
                        finalDetails = new
                        {
                            total = res.Total,
                            mismatches = res.Mismatches,
                            failures = res.Failures
                        };
                        break;
                    default:
                        Console.WriteLine($"Unknown test kind: {testKind}");
                        exitCode = 3;
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                await UpsertStatusAsync(connString, jobGuid, folderName, testKind, "Error", new { error = ex.Message });
                return 1;
            }

            var finalStatus = exitCode == 0 ? "Passed" : "Failed";
            await UpsertStatusAsync(connString, jobGuid, folderName, testKind, finalStatus,
                finalDetails ?? new { exitCode });
            Console.WriteLine($"Job {jobId}: Completed with status {finalStatus}");
            return exitCode;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private sealed record Md5PrefixResult(int ExitCode, int Total, int Mismatches, IReadOnlyList<object> Failures);

    private static async Task<Md5PrefixResult> RunFileHasCorrectMd5PrefixAsync(IFileSystem fs, string folder,
        Func<dynamic, Task> log)
    {
        if (!fs.DirectoryExists(folder))
        {
            await log(new { message = $"Folder does not exist: {folder}" });
            return new Md5PrefixResult(2, 0, 0, Array.Empty<object>());
        }

        var files = fs.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly).ToArray();
        int total = files.Length;
        int mismatches = 0;
        var failures = new List<object>();
        await log(new { message = $"Checking {total} files..." });

        foreach (var filePath in files)
        {
            if (!filePath.AllowImageToProcess())
                continue;

            var fileIdParts = Path.GetFileNameWithoutExtension(filePath).Split("_");
            string md5Hash = string.Empty;

            if (fileIdParts.Length == 4 && fileIdParts[0].Length == 4)
            {
                // we assume that fileIdParts[0] is a group ID, 4 characters long
                // then we assume that fileIdParts[3] is md5 hash
                md5Hash = fileIdParts[3];
            }

            if (fileIdParts.Length == 3)
            {
                // we assume that fileIdParts[0..1] are preview hashes
                // then we assume that fileIdParts[2] is md5 hash
                md5Hash = fileIdParts[2];
            }

            if (fileIdParts.Length == 1)
            {
                md5Hash = fileIdParts[0];
            }

            string actualHash = await filePath.CalculateMd5Async(force:true);

            var result = md5Hash.Equals(actualHash, StringComparison.InvariantCultureIgnoreCase);

            if (!result)
            {
                mismatches++;
                
                await log(new { message = $"MD5 hash is incorrect: {Path.GetFileName(filePath)}" });
                failures.Add(new { file = filePath, reason = "MD5 hash is incorrect" });
            }
        }

        await log(new { message = $"Done. Mismatches: {mismatches} of {total}" });
        var code = mismatches == 0 ? 0 : 4;
        return new Md5PrefixResult(code, total, mismatches, failures);
    }

    private static string? GetArgValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length) return args[i + 1];
                return null;
            }
        }

        return null;
    }

    private static string? DecodeB64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        try
        {
            var bytes = Convert.FromBase64String(value);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            // If not valid base64, return as-is for backward compatibility
            return value;
        }
    }

    private static async Task UpsertStatusAsync(string conn, Guid jobId, string folder, string testKind, string status,
        object details, bool startedAtOnly = false)
    {
        await using var npg = new NpgsqlConnection(conn);
        await npg.OpenAsync();
        var sql = startedAtOnly
            ? @"INSERT INTO content_validation_result (job_id, folder, test_kind, status, details)
                VALUES (@job_id, @folder, @test_kind, @status, @details::jsonb)
                ON CONFLICT (job_id, folder, test_kind) DO UPDATE SET
                    status = EXCLUDED.status,
                    details = EXCLUDED.details,
                    started_at = now()"
            : @"INSERT INTO content_validation_result (job_id, folder, test_kind, status, details, finished_at)
                VALUES (@job_id, @folder, @test_kind, @status, @details::jsonb, now())
                ON CONFLICT (job_id, folder, test_kind) DO UPDATE SET
                    status = EXCLUDED.status,
                    details = EXCLUDED.details,
                    finished_at = now()";

        await using var cmd = new NpgsqlCommand(sql, npg);
        cmd.Parameters.AddWithValue("@job_id", jobId);
        cmd.Parameters.AddWithValue("@folder", folder);
        cmd.Parameters.AddWithValue("@test_kind", testKind);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@details", JsonSerializer.Serialize(details));
        await cmd.ExecuteNonQueryAsync();
    }
}