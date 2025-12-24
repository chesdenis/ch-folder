namespace content_validator;

internal interface IContentValidationTest
{
    string Key { get; }
    Task<(int ExitCode, object Details)> ExecuteAsync(string folder, Func<dynamic, Task> log);
}