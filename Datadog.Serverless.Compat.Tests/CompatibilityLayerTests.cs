using System.Runtime.InteropServices;
using Xunit;

namespace Datadog.Serverless.Compat.Tests;

public class CompatibilityLayerTests
{
    [Fact]
    public void GetEnvironment_ShouldReturnAzureFunction_WhenAzureEnvironmentVariablesAreSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", "some_version");
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "some_runtime");

        // Act
        var result = CompatibilityLayer.GetEnvironment();

        // Assert
        Assert.Equal(CloudEnvironment.AzureFunction, result);

        // Cleanup
        Environment.SetEnvironmentVariable("FUNCTIONS_EXTENSION_VERSION", null);
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", null);
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
        Environment.SetEnvironmentVariable("WEBSITE_SKU", websiteSku);
        Environment.SetEnvironmentVariable("DD_AZURE_RESOURCE_GROUP", ddAzureResourceGroup);

        // Act
        var result = CompatibilityLayer.IsAzureFlexWithoutDDAzureResourceGroup();

        // Assert
        Assert.Equal(expected, result);

        // Cleanup
        Environment.SetEnvironmentVariable("WEBSITE_SKU", null);
        Environment.SetEnvironmentVariable("DD_AZURE_RESOURCE_GROUP", null);
    }

    [Theory]
    [InlineData(null, null, null, null, "dd_trace_", "dd_dogstatsd_")] // Default names
    [InlineData("custom_trace", "custom_dogstatsd", null, null, "custom_trace_", "custom_dogstatsd_")] // WINDOWS_PIPE_NAME set
    [InlineData(null, null, "other_trace", "other_dogstatsd", "other_trace_", "other_dogstatsd_")] // PIPE_NAME set
    public void ConfigureNamedPipes_ShouldConfigureCorrectly_OnWindows(
        string? traceWindowsPipeName,
        string? dogstatsdWindowsPipeName,
        string? tracePipeName,
        string? dogstatsdPipeName,
        string expectedTracePrefix,
        string expectedDogstatsdPrefix)
    {
        // Arrange
        var startInfo = new System.Diagnostics.ProcessStartInfo();
        const OS os = OS.Windows;

        Environment.SetEnvironmentVariable("DD_TRACE_WINDOWS_PIPE_NAME", traceWindowsPipeName);
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_WINDOWS_PIPE_NAME", dogstatsdWindowsPipeName);
        Environment.SetEnvironmentVariable("DD_TRACE_PIPE_NAME", tracePipeName);
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME", dogstatsdPipeName);

        // Act
        CompatibilityLayer.ConfigureNamedPipes(startInfo, os);

        // Assert
        var resultTracePipeName = Environment.GetEnvironmentVariable("DD_TRACE_PIPE_NAME");
        var resultDogstatsdPipeName = Environment.GetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME");

        Assert.NotNull(resultTracePipeName);
        Assert.NotNull(resultDogstatsdPipeName);
        Assert.StartsWith(expectedTracePrefix, resultTracePipeName);
        Assert.StartsWith(expectedDogstatsdPrefix, resultDogstatsdPipeName);
        Assert.Equal(resultTracePipeName, startInfo.EnvironmentVariables["DD_TRACE_WINDOWS_PIPE_NAME"]);
        Assert.Equal(resultDogstatsdPipeName, startInfo.EnvironmentVariables["DD_DOGSTATSD_WINDOWS_PIPE_NAME"]);

        // Cleanup
        Environment.SetEnvironmentVariable("DD_TRACE_PIPE_NAME", null);
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME", null);
        Environment.SetEnvironmentVariable("DD_TRACE_WINDOWS_PIPE_NAME", null);
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_WINDOWS_PIPE_NAME", null);
    }

    [Fact]
    public void ConfigureNamedPipes_ShouldNotConfigurePipes_OnLinux()
    {
        // Arrange
        var startInfo = new System.Diagnostics.ProcessStartInfo();
        const OS os = OS.Linux;

        // Clear any existing pipe name configurations
        Environment.SetEnvironmentVariable("DD_TRACE_PIPE_NAME", null);
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME", null);

        // Act
        CompatibilityLayer.ConfigureNamedPipes(startInfo, os);

        // Assert
        Assert.False(startInfo.EnvironmentVariables.ContainsKey("DD_TRACE_WINDOWS_PIPE_NAME"));
        Assert.False(startInfo.EnvironmentVariables.ContainsKey("DD_DOGSTATSD_WINDOWS_PIPE_NAME"));
    }

    [Fact]
    public void ConfigureNamedPipes_ShouldTruncateBaseName_WhenTooLong()
    {
        // Arrange
        var startInfo = new System.Diagnostics.ProcessStartInfo();
        const OS os = OS.Windows;
        var longPipeName = new string('a', 300); // 300 characters, exceeds max base length of 214

        Environment.SetEnvironmentVariable("DD_TRACE_WINDOWS_PIPE_NAME", longPipeName);
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_WINDOWS_PIPE_NAME", longPipeName);

        // Act
        CompatibilityLayer.ConfigureNamedPipes(startInfo, os);

        // Assert
        var tracePipeName = Environment.GetEnvironmentVariable("DD_TRACE_PIPE_NAME");
        var dogstatsdPipeName = Environment.GetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME");

        Assert.NotNull(tracePipeName);
        Assert.NotNull(dogstatsdPipeName);

        // Pipe name should be 247 chars: 214 (base) + 1 (underscore) + 32 (GUID)
        // Full path \\.\pipe\{name} would be 256 chars total
        Assert.Equal(247, tracePipeName.Length);
        Assert.Equal(247, dogstatsdPipeName.Length);

        // Cleanup
        Environment.SetEnvironmentVariable("DD_TRACE_PIPE_NAME", null);
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME", null);
        Environment.SetEnvironmentVariable("DD_TRACE_WINDOWS_PIPE_NAME", null);
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_WINDOWS_PIPE_NAME", null);
    }
}

