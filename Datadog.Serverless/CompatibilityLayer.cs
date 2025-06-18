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
    private enum CloudEnvironment
    {
        Unknown,
        AzureFunction,
    }

    private static readonly string OS = RuntimeInformation.OSDescription.ToLower();
    private static readonly ILogger Logger;

    private static string _homeDir = Path.DirectorySeparatorChar.ToString();

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

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(logLevel);
        });
        Logger = loggerFactory.CreateLogger("Datadog.Serverless");
    }

    private static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    private static CloudEnvironment GetEnvironment()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION")) &&
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME")))
        {
            _homeDir = Path.Combine(
                Path.DirectorySeparatorChar.ToString(),
                "home",
                "site",
                "wwwroot"
            );
            return CloudEnvironment.AzureFunction;
        }

        return CloudEnvironment.Unknown;
    }

    private static string GetBinaryPath()
    {
        var binaryPath = Environment.GetEnvironmentVariable("DD_SERVERLESS_COMPAT_PATH");

        if (!string.IsNullOrEmpty(binaryPath))
        {
            Logger.LogDebug("Detected user configured binary path {binaryPath}", binaryPath);
            return binaryPath;
        }

        if (IsWindows())
        {
            Logger.LogDebug("Detected {OS}", OS);
            return Path.Combine(
                _homeDir,
                "datadog",
                "bin",
                "windows-amd64",
                "datadog-serverless-compat.exe"
            );
        }
        else
        {
            Logger.LogDebug("Detected {OS}", OS);
            return Path.Combine(
                _homeDir,
                "datadog",
                "bin",
                "linux-amd64",
                "datadog-serverless-compat"
            );
        }
    }

    private static string GetPackageVersion()
    {
        try
        {
            return Assembly.GetExecutingAssembly()
                          .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                           ?.InformationalVersion ?? "unknown";
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to identify package version");
            return "unknown";
        }
    }

    public static void Start()
    {
        var environment = GetEnvironment();
        Logger.LogDebug("Environment detected: {Environment}", environment);

        if (environment == CloudEnvironment.Unknown)
        {
            Logger.LogError(
                "{Environment} environment detected, will not start the Datadog Serverless Compatibility Layer",
                environment
            );
            return;
        }

        if (!IsWindows() && !IsLinux())
        {
            Logger.LogError(
                "Platform {OS} detected, the Datadog Serverless Compatibility Layer is only supported on Windows and Linux",
                OS
            );
            return;
        }

        var binaryPath = GetBinaryPath();
        _logger.LogDebug("Spawning process from binary at path {binaryPath}", binaryPath);

        if (!File.Exists(binaryPath))
        {
            Logger.LogError(
                "Serverless Compatibility Layer did not start, could not find binary at path {binaryPath}",
                binaryPath
            );
            return;
        }

        var packageVersion = GetPackageVersion();
        Logger.LogDebug("Found package version {packageVersion}", packageVersion);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = binaryPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

            startInfo.EnvironmentVariables["DD_SERVERLESS_COMPAT_VERSION"] = packageVersion;

            var process = new Process { StartInfo = startInfo };
            process.Start();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception when starting {binaryPath}", binaryPath);
        }
    }
}
