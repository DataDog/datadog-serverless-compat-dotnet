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
}
