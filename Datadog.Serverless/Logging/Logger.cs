namespace Datadog.Serverless.Logging;

internal sealed class Logger : ILogger
{
    private readonly TextWriter _writer;
    private readonly string _source;
    private readonly LogLevel _minimumLevel;

    public Logger(TextWriter writer, string source, LogLevel minimumLevel)
    {
        _writer = writer;
        _source = source;
        _minimumLevel = minimumLevel;
    }

    public bool IsEnabled(LogLevel level)
    {
        return level >= _minimumLevel;
    }

    private void Log(LogLevel level, Exception? exception, string message)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        var levelString = level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRITICAL",
            _ => "UNKNOWN"
        };

        var logString = $"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff zzz} | {_source} | {levelString}] {message}";

        if (exception != null)
        {
            logString = $"{logString} | {ToSingleLineString(exception)}";
        }

        _writer.WriteLine(logString);
    }

    private static string ToSingleLineString(Exception exception)
    {
        var exceptionString = exception.ToString();

#if NET6_0_OR_GREATER
        return exceptionString.ReplaceLineEndings("\\n");
#else
        return exceptionString.Replace("\r\n", "\\n")
                              .Replace("\n", "\\n");
#endif
    }

    public void LogTrace(string message) => Log(LogLevel.Trace, exception: null, message);

    public void LogDebug(string message) => Log(LogLevel.Debug, exception: null, message);

    public void LogInformation(string message) => Log(LogLevel.Information, exception: null, message);

    public void LogWarning(string message) => Log(LogLevel.Warning, exception: null, message);

    public void LogWarning(Exception? exception, string message) => Log(LogLevel.Warning, exception, message);

    public void LogError(string message) => Log(LogLevel.Error, exception: null, message);

    public void LogError(Exception? exception, string message) => Log(LogLevel.Error, exception, message);

    public void LogCritical(string message) => Log(LogLevel.Critical, exception: null, message);

    public void LogCritical(Exception? exception, string message) => Log(LogLevel.Critical, exception, message);
}
