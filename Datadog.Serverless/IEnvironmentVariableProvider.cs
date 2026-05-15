// <copyright file="IEnvironmentVariableProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2025 Datadog, Inc.
// </copyright>

namespace Datadog.Serverless;

internal interface IEnvironmentVariableProvider
{
    string? GetEnvironmentVariable(string key);
}

internal readonly struct EnvironmentVariableProvider : IEnvironmentVariableProvider
{
    public string? GetEnvironmentVariable(string key)
        => Environment.GetEnvironmentVariable(key);
}
