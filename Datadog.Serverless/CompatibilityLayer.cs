// <copyright file="CompatibilityLayer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2025 Datadog, Inc.
// </copyright>

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Datadog.Serverless;

public static class CompatibilityLayer
{
    private static readonly ILogger Logger;

    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string filePath, uint mode);

    static CompatibilityLayer()
    {
        var logLevelEnv = Environment.GetEnvironmentVariable("DD_LOG_LEVEL");

        var logLevel = logLevelEnv?.ToUpper() switch
        {
            "OFF" => LogLevel.None,
            "CRITICAL" => LogLevel.Critical,
            "ERROR" => LogLevel.Error,
            "WARN" => LogLevel.Warning,
            "INFO" => LogLevel.Information,
            "DEBUG" => LogLevel.Debug,
            "TRACE" => LogLevel.Trace,
            _ => LogLevel.Information
        };

        var loggerFactory = LoggerFactory.Create(builder => { builder.AddConsole().SetMinimumLevel(logLevel); });
        Logger = loggerFactory.CreateLogger("Datadog.Serverless");
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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OS.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return OS.Linux;
        }

        return OS.Unknown;
    }

    internal static string GetExecutablePath(CloudEnvironment environment, OS os)
    {
        var binaryPath = Environment.GetEnvironmentVariable("DD_SERVERLESS_COMPAT_PATH");

        if (!string.IsNullOrEmpty(binaryPath))
        {
            Logger.LogDebug("Detected user configured binary path {binaryPath}", binaryPath);
            return binaryPath;
        }

        return (environment, os) switch
        {
            (CloudEnvironment.AzureFunction, OS.Windows) => @"C:\home\site\wwwroot\datadog\bin\windows-amd64\datadog-serverless-compat.exe",
            (CloudEnvironment.AzureFunction, OS.Linux) => "/home/site/wwwroot/datadog/bin/linux-amd64/datadog-serverless-compat",
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

    public static void Start()
    {
        var os = GetOs();
        var environment = GetEnvironment();
        var packageVersion = GetPackageVersion();
        var executablePath = GetExecutablePath(environment, os);

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("OS Description: {OSDescription}", RuntimeInformation.OSDescription.ToLower());
            Logger.LogDebug("Detected OS: {OS}", os);
            Logger.LogDebug("Detected cloud environment: {Environment}", environment);
            Logger.LogDebug("Package version: {PackageVersion}", packageVersion);
            Logger.LogDebug("Executable path: {ExecutablePath}", executablePath);
        }

        if (os == OS.Unknown)
        {
            Logger.LogError(
                "The Datadog Serverless Compatibility Layer does not support the detected OS: {OS}.",
                RuntimeInformation.OSDescription.ToLower());

            return;
        }

        if (environment == CloudEnvironment.Unknown)
        {
            Logger.LogError(
                "The Datadog Serverless Compatibility Layer does not support the detected cloud environment: {Environment}.",
                environment);

            return;
        }

        if (!File.Exists(executablePath))
        {
            Logger.LogError(
                "The Datadog Serverless Compatibility Layer executable was not found at path {executablePath}",
                executablePath);

            return;
        }

        Logger.LogDebug("Spawning process from executable at path {executablePath}", executablePath);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.EnvironmentVariables["DD_SERVERLESS_COMPAT_VERSION"] = packageVersion;

            var process = new Process { StartInfo = startInfo };
            process.Start();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception when starting {binaryPath}", executablePath);
        }
    }
}
