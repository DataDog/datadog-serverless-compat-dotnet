// <copyright file="DatadogServerlessCompat.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2025 Datadog, Inc.
// </copyright>

﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Datadog.Serverless.Compat
{
    internal enum CloudEnvironment
    {
        AzureFunction,
        AzureSpringApp,
        GoogleCloudRunFunction1stGen,
        Unknown
    }

    public static class DatadogServerlessCompat
    {
        private static readonly string OS = RuntimeInformation.OSDescription.ToLower();
        private static readonly string? BinaryPath = Environment.GetEnvironmentVariable(
            "DD_SERVERLESS_COMPAT_PATH"
        );
        private static readonly ILogger _logger;

        static DatadogServerlessCompat()
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
            _logger = loggerFactory.CreateLogger("Datadog.Serverless.Compat");
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
                return CloudEnvironment.AzureFunction;
            }

            if (env.Contains("FUNCTION_NAME") && env.Contains("GCP_PROJECT"))
            {
                return CloudEnvironment.GoogleCloudRunFunction1stGen;
            }

            return CloudEnvironment.Unknown;
        }

        private static string GetBinaryPath()
        {
            if (!string.IsNullOrEmpty(BinaryPath))
            {
                _logger.LogDebug("Detected user configured binary path {BinaryPath}", BinaryPath);
                return BinaryPath;
            }

            if (IsWindows())
            {
                _logger.LogDebug("Detected {OS}", OS);
                return "datadog/bin/windows-amd64/datadog-serverless-compat.exe";
            }
            else
            {
                _logger.LogDebug("Detected {OS}", OS);
                return "datadog/bin/linux-amd64/datadog-serverless-compat";
            }
        }

        private static string SetPackageVersion()
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

        public static void Start()
        {
            var environment = GetEnvironment();
            _logger.LogInformation("Environment detected: {Environment}", environment);

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
            _logger.LogDebug("Spawning process from binary at path {binaryPath}", binaryPath);

            if (!File.Exists(binaryPath))
            {
                _logger.LogError(
                    "Serverless Compatibility Layer did not start, could not find binary at path {binaryPath}",
                    binaryPath
                );
                return;
            }

            var packageVersion = SetPackageVersion();
            _logger.LogDebug("Found package version {packageVersion}", packageVersion);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = binaryPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
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
