namespace Datadog.Serverless;

internal interface ILogger
{
    bool IsEnabled(LogLevel level);

    void LogTrace(string? message);

    void LogDebug(string? message);

    void LogInformation(string? message);

    void LogWarning(string? message);

    void LogWarning(Exception? exception, string? message);

    void LogError(string? message);

    void LogError(Exception? exception, string? message);

    void LogCritical(string? message);

    void LogCritical(Exception? exception, string? message);
}
