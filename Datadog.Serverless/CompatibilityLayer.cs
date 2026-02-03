// <copyright file="CompatibilityLayer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2025 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Serverless.Logging;

namespace Datadog.Serverless;

public static class CompatibilityLayer
{
    private const string MutexName = "Global\\DatadogServerlessCompat";
    private static readonly ILogger Logger;
    private static Mutex? _mutex;

    [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int chmod(string filePath, uint mode);

    static CompatibilityLayer()
    {
        var logLevel = Logging.Logger.GetLogLevelFromEnvironment();
        Logger = new Logger(Console.Out, nameof(CompatibilityLayer), logLevel);
    }

    internal static CloudEnvironment GetEnvironment()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION")) &&
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME")))
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

    internal static string GetExecutablePath(CloudEnvironment environment, OS os)
    {
        var executablePath = Environment.GetEnvironmentVariable("DD_SERVERLESS_COMPAT_PATH");

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

    internal static bool IsAzureFlexWithoutDDAzureResourceGroup()
    {
        return Environment.GetEnvironmentVariable("WEBSITE_SKU") == "FlexConsumption" && Environment.GetEnvironmentVariable("DD_AZURE_RESOURCE_GROUP") == null;
    }

    public static void Start()
    {
        // Single-instance enforcement using named mutex
        try
        {
            _mutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out bool createdNew);

            if (!createdNew)
            {
                Logger.LogDebug("Another instance of the Datadog Serverless Compatibility Layer is already running. Skipping.");
                _mutex.Dispose();
                _mutex = null;
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create mutex for single-instance check. Proceeding anyway.");
            // Continue execution - better to risk duplicate than fail completely
        }

        // detect values
        var os = GetOs();
        var environment = GetEnvironment();
        var packageVersion = GetPackageVersion();
        var executablePath = GetExecutablePath(environment, os);

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

        if (environment == CloudEnvironment.AzureFunction && IsAzureFlexWithoutDDAzureResourceGroup())
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

            var process = new Process { StartInfo = startInfo };
            process.Start();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Exception when starting {executablePath}");
        }
    }
}
