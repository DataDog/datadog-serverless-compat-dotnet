// <copyright file="MockEnvironmentVariableProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2025 Datadog, Inc.
// </copyright>

namespace Datadog.Serverless.Compat.Tests;

internal class MockEnvironmentVariableProvider : IEnvironmentVariableProvider
{
    private readonly Dictionary<string, string> _variables = new();

    public void Set(string key, string? value)
    {
        if (value is null)
        {
            _variables.Remove(key);
        }
        else
        {
            _variables[key] = value;
        }
    }

    public string? GetEnvironmentVariable(string key)
        => _variables.TryGetValue(key, out var value) ? value : null;
}
