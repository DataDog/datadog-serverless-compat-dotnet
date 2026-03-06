using System.Runtime.InteropServices;
using Xunit;

namespace Datadog.Serverless.Compat.Tests;

/// <summary>
/// Tests that modify environment variables must not run in parallel to avoid test pollution.
/// </summary>
[Collection(nameof(EnvironmentVariablesTestCollection))]
public class CompatibilityLayerTests
{
    [Fact]
    public void GetEnvironment_ShouldReturnAzureFunction_WhenAzureEnvironmentVariablesAreSet()
    {
        // Arrange
        using var _ = new EnvironmentVariableScope(
            ("FUNCTIONS_EXTENSION_VERSION", "some_version"),
            ("FUNCTIONS_WORKER_RUNTIME", "some_runtime"));

        // Act
        var result = CompatibilityLayer.GetEnvironment();

        // Assert
        Assert.Equal(CloudEnvironment.AzureFunction, result);
    }

    [Fact]
    public void GetEnvironment_ShouldReturnUnknown_WhenNoEnvironmentVariablesAreSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", null);
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", null);

        // Act
        var result = CompatibilityLayer.GetEnvironment();

        // Assert
        Assert.Equal(CloudEnvironment.Unknown, result);
    }

    [Fact]
    public void GetOs_ShouldReturnCorrectOS()
    {
        // Act
        var result = CompatibilityLayer.GetOs();

        // Assert
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal(OS.Windows, result);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.Equal(OS.Linux, result);
        }
        else
        {
            Assert.Equal(OS.Unknown, result);
        }
    }

    [Fact]
    public void GetExecutablePath_ShouldReturnCorrectPath_ForWindows()
    {
        // Arrange
        const CloudEnvironment environment = CloudEnvironment.AzureFunction;
        const OS os = OS.Windows;

        // Act
        var result = CompatibilityLayer.GetExecutablePath(environment, os);

        // Assert
        Assert.Equal(@"C:\home\site\wwwroot\datadog\bin\windows-amd64\datadog-serverless-compat.exe", result);
    }

    [Fact]
    public void GetExecutablePath_ShouldReturnCorrectPath_ForLinux()
    {
        // Arrange
        const CloudEnvironment environment = CloudEnvironment.AzureFunction;
        const OS os = OS.Linux;

        // Act
        var result = CompatibilityLayer.GetExecutablePath(environment, os);

        // Assert
        Assert.Equal("/home/site/wwwroot/datadog/bin/linux-amd64/datadog-serverless-compat", result);
    }

    [Fact]
    public void GetPackageVersion_ShouldReturnVersion_WhenAssemblyAttributeExists()
    {
        // Act
        var result = CompatibilityLayer.GetPackageVersion();

        // Assert
        Assert.NotEqual("unknown", result);
    }

    [Theory]
    [InlineData("FlexConsumption", null, true)]
    [InlineData("FlexConsumption", "test-rg", false)]
    [InlineData("ElasticPremium", null, false)]
    [InlineData("ElasticPremium", "test-rg", false)]
    public void IsAzureFlexWithoutDDAzureResourceGroup_ShouldReturnCorrectValue(string websiteSku, string? ddAzureResourceGroup, bool expected)
    {
        // Arrange
        using var _ = new EnvironmentVariableScope(
            ("WEBSITE_SKU", websiteSku),
            ("DD_AZURE_RESOURCE_GROUP", ddAzureResourceGroup));

        // Act
        var result = CompatibilityLayer.IsAzureFlexWithoutDDAzureResourceGroup();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateTracePipeName_ShouldGenerateUniqueName_WhenNoEnvVarSet()
    {
        // Arrange
        using var _ = new EnvironmentVariableScope(
            ("DD_TRACE_WINDOWS_PIPE_NAME", null),
            ("DD_TRACE_PIPE_NAME", null));

        // Act
        var pipeName1 = CompatibilityLayer.CalculateTracePipeName();
        var pipeName2 = CompatibilityLayer.CalculateTracePipeName();

        // Assert
        Assert.StartsWith("dd_trace_", pipeName1);
        Assert.StartsWith("dd_trace_", pipeName2);
        Assert.NotEqual(pipeName1, pipeName2);
        Assert.Equal("dd_trace_".Length + 32, pipeName1.Length);
    }

    [Theory]
    [InlineData("DD_TRACE_WINDOWS_PIPE_NAME", "custom_trace")]
    [InlineData("DD_TRACE_PIPE_NAME", "custom_trace")]
    public void CalculateTracePipeName_ShouldReturnExactName_WhenEnvVarSet(string envVarName, string envVarValue)
    {
        // Arrange
        using var _ = new EnvironmentVariableScope(
            ("DD_TRACE_WINDOWS_PIPE_NAME", null),
            ("DD_TRACE_PIPE_NAME", null),
            (envVarName, envVarValue));

        // Act
        var pipeName = CompatibilityLayer.CalculateTracePipeName();

        // Assert — no GUID suffix when explicitly configured
        Assert.Equal("custom_trace", pipeName);
    }

    [Fact]
    public void CalculateDogStatsDPipeName_ShouldGenerateUniqueName_WhenNoEnvVarSet()
    {
        // Arrange
        using var _ = new EnvironmentVariableScope(
            ("DD_DOGSTATSD_WINDOWS_PIPE_NAME", null),
            ("DD_DOGSTATSD_PIPE_NAME", null));

        // Act
        var pipeName1 = CompatibilityLayer.CalculateDogStatsDPipeName();
        var pipeName2 = CompatibilityLayer.CalculateDogStatsDPipeName();

        // Assert
        Assert.StartsWith("dd_dogstatsd_", pipeName1);
        Assert.StartsWith("dd_dogstatsd_", pipeName2);
        Assert.NotEqual(pipeName1, pipeName2);
        Assert.Equal("dd_dogstatsd_".Length + 32, pipeName1.Length);
    }

    [Theory]
    [InlineData("DD_DOGSTATSD_WINDOWS_PIPE_NAME", "custom_dogstatsd")]
    [InlineData("DD_DOGSTATSD_PIPE_NAME", "custom_dogstatsd")]
    public void CalculateDogStatsDPipeName_ShouldReturnExactName_WhenEnvVarSet(string envVarName, string envVarValue)
    {
        // Arrange
        using var _ = new EnvironmentVariableScope(
            ("DD_DOGSTATSD_WINDOWS_PIPE_NAME", null),
            ("DD_DOGSTATSD_PIPE_NAME", null),
            (envVarName, envVarValue));

        // Act
        var pipeName = CompatibilityLayer.CalculateDogStatsDPipeName();

        // Assert — no GUID suffix when explicitly configured
        Assert.Equal("custom_dogstatsd", pipeName);
    }

    [Fact]
    public void CalculateTracePipeName_ShouldReturnExactName_EvenWhenLong()
    {
        // Arrange — explicit pipe names are returned as-is (no GUID, no truncation)
        var longPipeName = new string('a', 300);
        using var _ = new EnvironmentVariableScope(
            ("DD_TRACE_WINDOWS_PIPE_NAME", longPipeName),
            ("DD_TRACE_PIPE_NAME", null));

        // Act
        var pipeName = CompatibilityLayer.CalculateTracePipeName();

        // Assert
        Assert.Equal(longPipeName, pipeName);
    }

    [Fact]
    public void CalculateDogStatsDPipeName_ShouldReturnExactName_EvenWhenLong()
    {
        // Arrange — explicit pipe names are returned as-is (no GUID, no truncation)
        var longPipeName = new string('a', 300);
        using var _ = new EnvironmentVariableScope(
            ("DD_DOGSTATSD_WINDOWS_PIPE_NAME", longPipeName),
            ("DD_DOGSTATSD_PIPE_NAME", null));

        // Act
        var pipeName = CompatibilityLayer.CalculateDogStatsDPipeName();

        // Assert
        Assert.Equal(longPipeName, pipeName);
    }

    [Fact]
    public void ConfigureNamedPipes_ShouldGenerateGuidSuffixedNames_WhenNoEnvVarsSet()
    {
        // Arrange
        using var _ = new EnvironmentVariableScope(
            ("DD_TRACE_WINDOWS_PIPE_NAME", null),
            ("DD_TRACE_PIPE_NAME", null),
            ("DD_DOGSTATSD_WINDOWS_PIPE_NAME", null),
            ("DD_DOGSTATSD_PIPE_NAME", null));
        var startInfo = new System.Diagnostics.ProcessStartInfo();

        // Act
        CompatibilityLayer.ConfigureNamedPipes(startInfo, OS.Windows);

        // Assert — spawned process env vars
        var resultTracePipeName = startInfo.EnvironmentVariables["DD_APM_WINDOWS_PIPE_NAME"];
        var resultDogstatsdPipeName = startInfo.EnvironmentVariables["DD_DOGSTATSD_WINDOWS_PIPE_NAME"];

        Assert.NotNull(resultTracePipeName);
        Assert.NotNull(resultDogstatsdPipeName);
        Assert.StartsWith("dd_trace_", resultTracePipeName);
        Assert.StartsWith("dd_dogstatsd_", resultDogstatsdPipeName);

        // Assert — current process env vars match (for in-process consumers like DogStatsD client)
        Assert.Equal(resultTracePipeName, Environment.GetEnvironmentVariable("DD_TRACE_PIPE_NAME"));
        Assert.Equal(resultDogstatsdPipeName, Environment.GetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME"));
    }

    [Fact]
    public void ConfigureNamedPipes_ShouldUseExactNames_WhenEnvVarsSet()
    {
        // Arrange
        using var _ = new EnvironmentVariableScope(
            ("DD_TRACE_WINDOWS_PIPE_NAME", "custom_trace"),
            ("DD_TRACE_PIPE_NAME", null),
            ("DD_DOGSTATSD_WINDOWS_PIPE_NAME", "custom_dogstatsd"),
            ("DD_DOGSTATSD_PIPE_NAME", null));
        var startInfo = new System.Diagnostics.ProcessStartInfo();

        // Act
        CompatibilityLayer.ConfigureNamedPipes(startInfo, OS.Windows);

        // Assert — spawned process env vars
        Assert.Equal("custom_trace", startInfo.EnvironmentVariables["DD_APM_WINDOWS_PIPE_NAME"]);
        Assert.Equal("custom_dogstatsd", startInfo.EnvironmentVariables["DD_DOGSTATSD_WINDOWS_PIPE_NAME"]);

        // Assert — current process env vars match
        Assert.Equal("custom_trace", Environment.GetEnvironmentVariable("DD_TRACE_PIPE_NAME"));
        Assert.Equal("custom_dogstatsd", Environment.GetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME"));
    }

    [Fact]
    public void ConfigureNamedPipes_ShouldNotConfigurePipes_OnLinux()
    {
        // Arrange
        using var _ = new EnvironmentVariableScope(
            ("DD_TRACE_PIPE_NAME", null),
            ("DD_DOGSTATSD_PIPE_NAME", null));
        var startInfo = new System.Diagnostics.ProcessStartInfo();
        const OS os = OS.Linux;

        // Act
        CompatibilityLayer.ConfigureNamedPipes(startInfo, os);

        // Assert
        Assert.False(startInfo.EnvironmentVariables.ContainsKey("DD_TRACE_WINDOWS_PIPE_NAME"));
        Assert.False(startInfo.EnvironmentVariables.ContainsKey("DD_DOGSTATSD_WINDOWS_PIPE_NAME"));
        Assert.Null(Environment.GetEnvironmentVariable("DD_TRACE_PIPE_NAME"));
        Assert.Null(Environment.GetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME"));
    }
}

/// <summary>
/// Helper class to temporarily set environment variables and automatically restore them on disposal.
/// Ensures proper cleanup even if tests fail, preventing test pollution.
/// </summary>
internal sealed class EnvironmentVariableScope : IDisposable
{
    private readonly Dictionary<string, string?> _originalValues = new();

    public EnvironmentVariableScope(params (string name, string? value)[] variables)
    {
        foreach (var (name, value) in variables)
        {
            _originalValues[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    public void Dispose()
    {
        foreach (var kvp in _originalValues)
        {
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
        }
    }
}

/// <summary>
/// Used to indicate tests that modify environment variables.
/// Tests in this collection will not run in parallel to prevent cross-test pollution.
/// </summary>
[CollectionDefinition(nameof(EnvironmentVariablesTestCollection), DisableParallelization = true)]
public class EnvironmentVariablesTestCollection
{
}

