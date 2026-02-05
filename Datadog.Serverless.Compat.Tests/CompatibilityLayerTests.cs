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
    [InlineData(null, "dd_trace_")] // Default name
    [InlineData("custom_trace", "custom_trace_")] // WINDOWS_PIPE_NAME set
    public void CalculateTracePipeName_ShouldGenerateUniqueName(string? traceWindowsPipeName, string expectedPrefix)
    {
        // Arrange
        Environment.SetEnvironmentVariable("DD_TRACE_WINDOWS_PIPE_NAME", traceWindowsPipeName);

        // Act
        var pipeName1 = CompatibilityLayer.CalculateTracePipeName();
        var pipeName2 = CompatibilityLayer.CalculateTracePipeName();

        // Assert
        Assert.NotNull(pipeName1);
        Assert.NotNull(pipeName2);
        Assert.StartsWith(expectedPrefix, pipeName1);
        Assert.StartsWith(expectedPrefix, pipeName2);

        // Each call should generate a unique GUID
        Assert.NotEqual(pipeName1, pipeName2);

        // Format should be: {base}_{guid}
        // GUID is 32 characters (N format)
        Assert.Equal(expectedPrefix.Length + 32, pipeName1.Length);

        // Cleanup
        Environment.SetEnvironmentVariable("DD_TRACE_WINDOWS_PIPE_NAME", null);
    }

    [Theory]
    [InlineData(null, "dd_dogstatsd_")] // Default name
    [InlineData("custom_dogstatsd", "custom_dogstatsd_")] // WINDOWS_PIPE_NAME set
    public void CalculateDogStatsDPipeName_ShouldGenerateUniqueName(string? dogstatsdWindowsPipeName, string expectedPrefix)
    {
        // Arrange
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_WINDOWS_PIPE_NAME", dogstatsdWindowsPipeName);

        // Act
        var pipeName1 = CompatibilityLayer.CalculateDogStatsDPipeName();
        var pipeName2 = CompatibilityLayer.CalculateDogStatsDPipeName();

        // Assert
        Assert.NotNull(pipeName1);
        Assert.NotNull(pipeName2);
        Assert.StartsWith(expectedPrefix, pipeName1);
        Assert.StartsWith(expectedPrefix, pipeName2);

        // Each call should generate a unique GUID
        Assert.NotEqual(pipeName1, pipeName2);

        // Format should be: {base}_{guid}
        Assert.Equal(expectedPrefix.Length + 32, pipeName1.Length);

        // Cleanup
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_WINDOWS_PIPE_NAME", null);
    }

    [Fact]
    public void CalculateTracePipeName_ShouldTruncateBaseName_WhenTooLong()
    {
        // Arrange
        var longPipeName = new string('a', 300); // Exceeds max base length of 214
        Environment.SetEnvironmentVariable("DD_TRACE_WINDOWS_PIPE_NAME", longPipeName);

        // Act
        var pipeName = CompatibilityLayer.CalculateTracePipeName();

        // Assert
        Assert.NotNull(pipeName);
        // Pipe name should be 247 chars: 214 (base) + 1 (underscore) + 32 (GUID)
        Assert.Equal(247, pipeName.Length);

        // Cleanup
        Environment.SetEnvironmentVariable("DD_TRACE_WINDOWS_PIPE_NAME", null);
    }

    [Fact]
    public void CalculateDogStatsDPipeName_ShouldTruncateBaseName_WhenTooLong()
    {
        // Arrange
        var longPipeName = new string('a', 300); // Exceeds max base length of 214
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_WINDOWS_PIPE_NAME", longPipeName);

        // Act
        var pipeName = CompatibilityLayer.CalculateDogStatsDPipeName();

        // Assert
        Assert.NotNull(pipeName);
        // Pipe name should be 247 chars: 214 (base) + 1 (underscore) + 32 (GUID)
        Assert.Equal(247, pipeName.Length);

        // Cleanup
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_WINDOWS_PIPE_NAME", null);
    }

    [Theory]
    [InlineData(null, null, "dd_trace_", "dd_dogstatsd_")] // Default names
    [InlineData("custom_trace", "custom_dogstatsd", "custom_trace_", "custom_dogstatsd_")] // WINDOWS_PIPE_NAME set
    public void ConfigureNamedPipes_ShouldPassPipeNamesToStartInfo_OnWindows(
        string? traceWindowsPipeName,
        string? dogstatsdWindowsPipeName,
        string expectedTracePrefix,
        string expectedDogstatsdPrefix)
    {
        // Arrange
        var startInfo = new System.Diagnostics.ProcessStartInfo();
        const OS os = OS.Windows;

        Environment.SetEnvironmentVariable("DD_TRACE_WINDOWS_PIPE_NAME", traceWindowsPipeName);
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_WINDOWS_PIPE_NAME", dogstatsdWindowsPipeName);

        // Act
        CompatibilityLayer.ConfigureNamedPipes(startInfo, os);

        // Assert
        var resultTracePipeName = startInfo.EnvironmentVariables["DD_TRACE_WINDOWS_PIPE_NAME"];
        var resultDogstatsdPipeName = startInfo.EnvironmentVariables["DD_DOGSTATSD_WINDOWS_PIPE_NAME"];

        Assert.NotNull(resultTracePipeName);
        Assert.NotNull(resultDogstatsdPipeName);
        Assert.StartsWith(expectedTracePrefix, resultTracePipeName);
        Assert.StartsWith(expectedDogstatsdPrefix, resultDogstatsdPipeName);

        // Cleanup
        Environment.SetEnvironmentVariable("DD_TRACE_WINDOWS_PIPE_NAME", null);
        Environment.SetEnvironmentVariable("DD_DOGSTATSD_WINDOWS_PIPE_NAME", null);
    }

    [Fact]
    public void ConfigureNamedPipes_ShouldNotConfigurePipes_OnLinux()
    {
        // Arrange
        var startInfo = new System.Diagnostics.ProcessStartInfo();
        const OS os = OS.Linux;

        // Act
        CompatibilityLayer.ConfigureNamedPipes(startInfo, os);

        // Assert
        Assert.False(startInfo.EnvironmentVariables.ContainsKey("DD_TRACE_WINDOWS_PIPE_NAME"));
        Assert.False(startInfo.EnvironmentVariables.ContainsKey("DD_DOGSTATSD_WINDOWS_PIPE_NAME"));
    }
}

