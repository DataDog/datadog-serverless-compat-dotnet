using Xunit;
using Datadog.Serverless.Logging;

namespace Datadog.Serverless.Compat.Tests.Logging;

public class LoggerTests
{
    public static TheoryData<int, int, bool> LogLevels
    {
        get
        {
            var data = new TheoryData<int, int, bool>();
            var logLevels = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().ToList();

            foreach (var minimumLevel in logLevels)
            {
                foreach (var checkLevel in logLevels)
                {
                    if (checkLevel == LogLevel.None)
                    {
                        // we can't generate logs with level "None", so skip this combination
                        continue;
                    }

                    data.Add((int)minimumLevel, (int)checkLevel, checkLevel >= minimumLevel);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(LogLevels))]
    public void IsEnabled_ReturnsCorrectValue(int minimumLevel, int checkLevel, bool expected)
    {
        var logger = new Logger(TextWriter.Null, "TestSource", (LogLevel)minimumLevel);

        Assert.Equal(expected, logger.IsEnabled((LogLevel)checkLevel));
    }

    [Fact]
    public void Log_IncludesCorrectComponents()
    {
        using var writer = new StringWriter();
        var logger = new Logger(writer, "TestSource", LogLevel.Debug);
        const string message = "Test message";

        logger.LogInformation(message);

        var output = writer.ToString();
        Assert.Matches(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \+00:00 \| TestSource \| INFO\] Test message\r?\n$", output);
    }

    [Fact]
    public void Log_WithException_IncludesExceptionDetails()
    {
        using var writer = new StringWriter();
        var logger = new Logger(writer, "TestSource", LogLevel.Debug);
        Exception exception;

        try
        {
            throw new InvalidOperationException("Test exception");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        logger.LogError(exception, "Error occurred");

        var output = writer.ToString();
        Assert.Matches(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \+00:00 \| TestSource \| ERROR\] Error occurred \| System.InvalidOperationException: Test exception\\n(.+)\r?\n$", output);
    }

    [Fact]
    public void Log_BelowMinimumLevel_DoesNotWrite()
    {
        using var writer = new StringWriter();
        var logger = new Logger(writer, "TestSource", LogLevel.Error);

        logger.LogDebug("Debug message");
        logger.LogInformation("Info message");
        logger.LogWarning("Warning message");

        var output = writer.ToString();
        Assert.Empty(output);

        logger.LogError("Error message");

        output = writer.ToString();
        Assert.NotEmpty(output);
    }
}
