using Datadog.Serverless;
using Datadog.Serverless.Logging;

// https://github.com/dotnet/runtime/blob/main/docs/design/features/host-startup-hook.md

// ReSharper disable once UnusedType.Global
// ReSharper disable once CheckNamespace
internal static class StartupHook
{
    // ReSharper disable once UnusedMember.Global
    public static void Initialize()
    {
        Logger? logger = null;

        try
        {
            var logLevel = Logger.GetLogLevelFromEnvironment(new EnvironmentVariableProvider());
            logger = new Logger(Console.Out, nameof(StartupHook), logLevel);
            logger.LogInformation("Starting the Datadog Serverless Compatibility Layer.");

            CompatibilityLayer.Start();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error starting the Datadog Serverless Compatibility Layer.");
        }
    }
}
