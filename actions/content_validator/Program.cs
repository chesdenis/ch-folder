using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Text.Json;
using shared_csharp.Abstractions;
using shared_csharp.Infrastructure;
using System.Linq;
using content_validator.ContentTests;

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
            string testKind = DecodeB64(GetArgValue(args, "--test-kind")) ?? throw new Exception("Missing --test-kind");
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
            // Register validation strategies
            services.AddSingleton<IContentValidationTest, ValidateMd5Prefix>();
            services.AddSingleton<IContentValidationTest, ValidateFileNameConvention>();
            
            services.AddSingleton<IContentValidationTest, ValidateDescriptionQuery>();
            services.AddSingleton<IContentValidationTest, ValidateEngShortQuery>();
            services.AddSingleton<IContentValidationTest, ValidateCommerceMarkQuery>();
            services.AddSingleton<IContentValidationTest, ValidateTagsQuery>();

            services.AddSingleton<IContentValidationTest, CheckCommerceJsonInAnswers>();
            services.AddSingleton<IContentValidationTest, ValidateDescriptionAnswerMustHaveMultipleSentences>();
            services.AddSingleton<IContentValidationTest, ConversationsMustHaveFileKey>();
            services.AddSingleton<IContentValidationTest, EmbeddingAnswersMustBeInsideConversation>();
            services.AddSingleton<IContentValidationTest, QuestionsMustHaveFileKey>();
            services.AddSingleton<IContentValidationTest, ValidateAnswersMustBeInsideConversation>();
            services.AddSingleton<IContentValidationTest, ValidateEmbeddingsResults>();
            services.AddSingleton<IContentValidationTest, ValidatePreviews>();
            
            await using var provider = services.BuildServiceProvider();

            var strategies = provider.GetServices<IContentValidationTest>();
            var strategy = strategies.FirstOrDefault(s => string.Equals(s.Key, testKind, StringComparison.OrdinalIgnoreCase));

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
                if (strategy is null)
                {
                    Console.WriteLine($"Unknown test kind: {testKind}");
                    exitCode = 3;
                }
                else
                {
                    var result = await strategy.ExecuteAsync(folder, async details => { Console.WriteLine(details.message); });
                    exitCode = result.ExitCode;
                    finalDetails = result.Details;
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

    // Strategy-specific code moved into FileHasCorrectMd5PrefixTest

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