// <copyright file="CompatibilityLayer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2025 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Datadog.Serverless
{
    internal enum CloudEnvironment
    {
        AzureFunction,
        Unknown
    }

    public static class CompatibilityLayer
    {
        private static readonly string OS = RuntimeInformation.OSDescription.ToLower();
        private static readonly ILogger _logger;
        private static string homeDir = Path.DirectorySeparatorChar.ToString();

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
            _logger = loggerFactory.CreateLogger("Datadog.Serverless");
        }

        private static bool IsWindows()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        private static bool IsLinux()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        }

        private static CloudEnvironment GetEnvironment()
        {
            var env = Environment.GetEnvironmentVariables();

            if (
                env.Contains("FUNCTIONS_EXTENSION_VERSION")
                && env.Contains("FUNCTIONS_WORKER_RUNTIME")
            )
            {
                homeDir = Path.Combine(
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
                _logger.LogDebug("Detected user configured binary path {binaryPath}", binaryPath);
                return binaryPath;
            }

            if (IsWindows())
            {
                _logger.LogDebug("Detected {OS}", OS);
                return Path.Combine(
                    homeDir,
                    "datadog",
                    "bin",
                    "windows-amd64",
                    "datadog-serverless-compat.exe"
                );
            }
            else
            {
                _logger.LogDebug("Detected {OS}", OS);
                return Path.Combine(
                    homeDir,
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
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;
                return version ?? "unknown";
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to identify package version");
                return "unknown";
            }
        }

        private static void SetFilePermissions(string filePath)
        {
            if (IsWindows())
                return;

            int result = chmod(filePath, 0x1E4); // Octal 0744
            if (result != 0)
            {
                int errno = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"chmod failed with errno {errno}");
            }
        }

        public static void Start()
        {
            var environment = GetEnvironment();
            _logger.LogDebug("Environment detected: {Environment}", environment);

            if (environment == CloudEnvironment.Unknown)
            {
                _logger.LogError(
                    "{Environment} environment detected, will not start the Datadog Serverless Compatibility Layer",
                    environment
                );
                return;
            }

            if (!IsWindows() && !IsLinux())
            {
                _logger.LogError(
                    "Platform {OS} detected, the Datadog Serverless Compatibility Layer is only supported on Windows and Linux",
                    OS
                );
                return;
            }

            var binaryPath = GetBinaryPath();

            if (!File.Exists(binaryPath))
            {
                _logger.LogError(
                    "Serverless Compatibility Layer did not start, could not find binary at path {binaryPath}",
                    binaryPath
                );
                return;
            }

            var packageVersion = GetPackageVersion();
            _logger.LogDebug("Found package version {packageVersion}", packageVersion);

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "datadog");
                Directory.CreateDirectory(tempDir);
                string executableFilePath = Path.Combine(tempDir, Path.GetFileName(binaryPath));
                File.Copy(binaryPath, executableFilePath, overwrite: true);
                SetFilePermissions(executableFilePath);
                _logger.LogDebug(
                    "Spawning process from binary at path {executableFilePath}",
                    executableFilePath
                );

                var startInfo = new ProcessStartInfo
                {
                    FileName = executableFilePath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                startInfo.EnvironmentVariables["DD_SERVERLESS_COMPAT_VERSION"] = packageVersion;

                var process = new Process { StartInfo = startInfo };
                process.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when starting {binaryPath}", binaryPath);
            }
        }
    }
}
