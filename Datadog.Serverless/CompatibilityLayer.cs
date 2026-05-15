// <copyright file="CompatibilityLayer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2025 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Datadog.Serverless.Logging;

namespace Datadog.Serverless;

public static class CompatibilityLayer
{
    private static readonly ILogger Logger;

    [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int chmod(string filePath, uint mode);

    static CompatibilityLayer()
    {
        var logLevel = Logging.Logger.GetLogLevelFromEnvironment(new EnvironmentVariableProvider());
        Logger = new Logger(Console.Out, nameof(CompatibilityLayer), logLevel);
    }

    internal static CloudEnvironment GetEnvironment(IEnvironmentVariableProvider envVars)
    {
        if (!string.IsNullOrEmpty(envVars.GetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION")) &&
            !string.IsNullOrEmpty(envVars.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME")))
        {
            return CloudEnvironment.AzureFunction;
        }

        return CloudEnvironment.Unknown;
    }

    internal static OS GetOs()
    {
#if NETFRAMEWORK
        // RuntimeInformation was added in net471, but we target net461
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            // this should always be true since .NET Framework only runs on Windows,
            // but it doesn't hurt to check (hello Mono).
            return OS.Windows;
        }
#else
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OS.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return OS.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return OS.MacOS;
        }
#endif

        return OS.Unknown;
    }

    internal static string GetExecutablePath(CloudEnvironment environment, OS os, IEnvironmentVariableProvider envVars)
    {
        var executablePath = envVars.GetEnvironmentVariable("DD_SERVERLESS_COMPAT_PATH");

        if (!string.IsNullOrEmpty(executablePath))
        {
            Logger.LogDebug($"Detected user-configured executable path DD_SERVERLESS_COMPAT_PATH={executablePath}");
            return executablePath;
        }

        return environment switch
        {
            CloudEnvironment.AzureFunction => os switch
            {
                OS.Windows => @"C:\home\site\wwwroot\datadog\bin\windows-amd64\datadog-serverless-compat.exe",
                OS.Linux => "/home/site/wwwroot/datadog/bin/linux-amd64/datadog-serverless-compat",
                _ => string.Empty
            },
            _ => string.Empty
        };
    }

    internal static string GetPackageVersion()
    {
        try
        {
            return Assembly.GetExecutingAssembly()
                           .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                           ?.InformationalVersion ?? "unknown";
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Unable to identify package version");
            return "unknown";
        }
    }

    internal static bool TryCopyExecutable(string sourceFilename, out string destinationFilename)
    {
        destinationFilename = string.Empty;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "datadog");
            destinationFilename = Path.Combine(tempDir, Path.GetFileName(sourceFilename));
            Directory.CreateDirectory(tempDir);
            File.Copy(sourceFilename, destinationFilename, overwrite: true);

            Logger.LogDebug($"Copied executable from {sourceFilename} to {destinationFilename}");
            return true;
        }
        catch (Exception e)
        {
            Logger.LogError(e, $"Failed to copy executable from {sourceFilename} to {sourceFilename}");
            return false;
        }
    }

    internal static bool TrySetFilePermissions(string filePath)
    {
        try
        {
            var result = chmod(filePath, 0x1E4); // Octal 0744

            if (result == 0)
            {
                Logger.LogDebug($"Changed permissions to 0744 for {filePath}");
                return true;
            }

            var errno = Marshal.GetLastWin32Error();
            Logger.LogError($"chmod failed with errno {errno}");
        }
        catch (Exception e)
        {
            Logger.LogError(e, "chmod failed");
        }

        return false;
    }

    internal static bool IsAzureFlexWithoutDDAzureResourceGroup(IEnvironmentVariableProvider envVars)
    {
        return envVars.GetEnvironmentVariable("WEBSITE_SKU") == "FlexConsumption" && envVars.GetEnvironmentVariable("DD_AZURE_RESOURCE_GROUP") == null;
    }

    private static string? DeterminePipeBaseName(string windowsPipeNameKey, string pipeNameKey, IEnvironmentVariableProvider envVars)
    {
        var windowsPipeName = envVars.GetEnvironmentVariable(windowsPipeNameKey);
        var pipeName = envVars.GetEnvironmentVariable(pipeNameKey);

        if (!string.IsNullOrEmpty(windowsPipeName))
        {
            if (!string.IsNullOrEmpty(pipeName) && pipeName != windowsPipeName)
            {
                Logger.LogWarning($"{pipeNameKey} ('{pipeName}') differs from {windowsPipeNameKey} ('{windowsPipeName}'). Using {windowsPipeNameKey}.");
            }
            return windowsPipeName;
        }

        if (!string.IsNullOrEmpty(pipeName))
        {
            return pipeName;
        }

        return null;
    }

    /// <summary>
    /// Calculates the trace pipe name with a unique GUID suffix.
    /// When the Datadog tracer is present, its calltarget instrumentation overrides the
    /// return value with the tracer's pre-generated pipe name, so both sides use the same name.
    /// When no tracer is present, this method generates its own unique pipe name.
    ///
    /// *** INSTRUMENTATION CONTRACT — DO NOT RENAME OR CHANGE SIGNATURE ***
    /// The dd-trace-dotnet tracer targets this exact symbol at runtime:
    ///   Assembly  : Datadog.Serverless.Compat
    ///   Type      : Datadog.Serverless.CompatibilityLayer
    ///   Method    : CalculateTracePipeName
    ///   Parameters: (none)
    ///   Returns   : System.String
    ///   Versions  : 0.0.0 – 1.*.*
    /// Renaming, moving, or adding parameters silently disables the integration —
    /// the native profiler skips unrecognised symbols without throwing.
    /// A 2.x major bump also escapes the version range and requires a coordinated tracer update.
    /// </summary>
    /// <returns>The trace pipe name to use for communication with the agent</returns>
    public static string CalculateTracePipeName()
        => CalculateTracePipeName(new EnvironmentVariableProvider());

    internal static string CalculateTracePipeName(IEnvironmentVariableProvider envVars)
    {
        var explicitName = DeterminePipeBaseName(
            "DD_TRACE_WINDOWS_PIPE_NAME",
            "DD_TRACE_PIPE_NAME",
            envVars);

        if (explicitName != null)
        {
            Logger.LogDebug($"Using explicitly configured trace pipe name: {explicitName}");
            return explicitName;
        }

        var pipeName = $"dd_trace_{Guid.NewGuid():N}";
        Logger.LogDebug($"CompatibilityLayer calculated trace pipe name: {pipeName}");
        return pipeName;
    }

    /// <summary>
    /// Calculates the DogStatsD pipe name with a unique GUID suffix.
    /// When the Datadog tracer is present, its calltarget instrumentation overrides the
    /// return value with the tracer's pre-generated pipe name, so both sides use the same name.
    /// When no tracer is present, this method generates its own unique pipe name.
    ///
    /// *** INSTRUMENTATION CONTRACT — DO NOT RENAME OR CHANGE SIGNATURE ***
    /// The dd-trace-dotnet tracer targets this exact symbol at runtime:
    ///   Assembly  : Datadog.Serverless.Compat
    ///   Type      : Datadog.Serverless.CompatibilityLayer
    ///   Method    : CalculateDogStatsDPipeName
    ///   Parameters: (none)
    ///   Returns   : System.String
    ///   Versions  : 0.0.0 – 1.*.*
    /// Renaming, moving, or adding parameters silently disables the integration —
    /// the native profiler skips unrecognised symbols without throwing.
    /// A 2.x major bump also escapes the version range and requires a coordinated tracer update.
    /// </summary>
    /// <returns>The DogStatsD pipe name to use for communication with the agent</returns>
    public static string CalculateDogStatsDPipeName()
        => CalculateDogStatsDPipeName(new EnvironmentVariableProvider());

    internal static string CalculateDogStatsDPipeName(IEnvironmentVariableProvider envVars)
    {
        var explicitName = DeterminePipeBaseName(
            "DD_DOGSTATSD_WINDOWS_PIPE_NAME",
            "DD_DOGSTATSD_PIPE_NAME",
            envVars);

        if (explicitName != null)
        {
            Logger.LogDebug($"Using explicitly configured DogStatsD pipe name: {explicitName}");
            return explicitName;
        }

        var pipeName = $"dd_dogstatsd_{Guid.NewGuid():N}";
        Logger.LogDebug($"CompatibilityLayer calculated DogStatsD pipe name: {pipeName}");
        return pipeName;
    }

    internal static void ConfigureNamedPipes(ProcessStartInfo startInfo, OS os)
    {
        // Only configure named pipes for Windows
        if (os != OS.Windows)
        {
            return;
        }

        // Call the public methods that can be instrumented by the tracer
        // If tracer is present: instrumentation will override the return values
        // If no tracer: methods will generate their own unique pipe names
        var tracePipeName = CalculateTracePipeName();
        var dogstatsdPipeName = CalculateDogStatsDPipeName();

        // The trace pipe name flows tracer → DD_APM_WINDOWS_PIPE_NAME → mini-agent.
        // The tracer reads its own ExporterSettings (already set before this hook fires)
        // and the mini-agent reads DD_APM_WINDOWS_PIPE_NAME from its spawned-process env.
        // There is no in-process consumer of DD_TRACE_PIPE_NAME at this stage.
        startInfo.EnvironmentVariables["DD_APM_WINDOWS_PIPE_NAME"] = tracePipeName;
        startInfo.EnvironmentVariables["DD_DOGSTATSD_WINDOWS_PIPE_NAME"] = dogstatsdPipeName;

        // Expose the DogStatsD pipe name in the current process so the DogStatsD client SDK
        // can discover it. DogStatsdService.Configure() is called lazily in user code after
        // this hook runs, so the env var will be visible when it reads DD_DOGSTATSD_PIPE_NAME.
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME", dogstatsdPipeName);

        Logger.LogInformation($"Configured named pipes - Trace: {tracePipeName}, DogStatsD: {dogstatsdPipeName}");
    }

    public static void Start()
    {
        var envVars = new EnvironmentVariableProvider();

        // detect values
        var os = GetOs();
        var environment = GetEnvironment(envVars);
        var packageVersion = GetPackageVersion();
        var executablePath = GetExecutablePath(environment, os, envVars);

        // log detected values
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug($"Detected OS: {os}");
            Logger.LogDebug($"Detected cloud environment: {environment}");
            Logger.LogDebug($"Package version: {packageVersion}");
            Logger.LogDebug($"Executable path: {executablePath}");
        }

        // validate each value and bail out if any are invalid
        if (os is not (OS.Windows or OS.Linux))
        {
            Logger.LogError(
                $"The Datadog Serverless Compatibility Layer does not support the detected OS: {os}.");

            return;
        }

        if (environment == CloudEnvironment.Unknown)
        {
            Logger.LogError(
                $"The Datadog Serverless Compatibility Layer does not support the detected cloud environment: {environment}.");

            return;
        }

        if (environment == CloudEnvironment.AzureFunction && IsAzureFlexWithoutDDAzureResourceGroup(envVars))
        {
            Logger.LogError(
                "Azure function detected on flex consumption plan without DD_AZURE_RESOURCE_GROUP set. Please set the DD_AZURE_RESOURCE_GROUP environment variable to your resource group name in Azure app settings. Shutting down Datadog Serverless Compatibility Layer.");
            
            return;
        }

        if (!File.Exists(executablePath))
        {
            Logger.LogError(
                $"The Datadog Serverless Compatibility Layer executable was not found at path {executablePath}");

            return;
        }

        if (os == OS.Linux)
        {
            if (TryCopyExecutable(executablePath, out var tempExecutablePath))
            {
                executablePath = tempExecutablePath;
            }
            else
            {
                return;
            }

            if (!TrySetFilePermissions(tempExecutablePath))
            {
                return;
            }
        }

        Logger.LogDebug($"Spawning process from executable at path {executablePath}");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.EnvironmentVariables["DD_SERVERLESS_COMPAT_VERSION"] = packageVersion;

            // Configure named pipes with unique names to avoid conflicts in multi-function scenarios
            ConfigureNamedPipes(startInfo, os);

            var process = new Process { StartInfo = startInfo };
            process.Start();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Exception when starting {executablePath}");
        }
    }
}
