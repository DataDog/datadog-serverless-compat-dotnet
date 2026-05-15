using System.Runtime.InteropServices;
using Xunit;

namespace Datadog.Serverless.Compat.Tests;

public class CompatibilityLayerTests
{
    [Fact]
    public void GetEnvironment_ShouldReturnAzureFunction_WhenAzureEnvironmentVariablesAreSet()
    {
        var envVars = new MockEnvironmentVariableProvider();
        envVars.Set("FUNCTIONS_EXTENSION_VERSION", "some_version");
        envVars.Set("FUNCTIONS_WORKER_RUNTIME", "some_runtime");

        var result = CompatibilityLayer.GetEnvironment(envVars);

        Assert.Equal(CloudEnvironment.AzureFunction, result);
    }

    [Fact]
    public void GetEnvironment_ShouldReturnUnknown_WhenNoEnvironmentVariablesAreSet()
    {
        var result = CompatibilityLayer.GetEnvironment(new MockEnvironmentVariableProvider());

        Assert.Equal(CloudEnvironment.Unknown, result);
    }

    [Fact]
    public void GetOs_ShouldReturnCorrectOS()
    {
        var result = CompatibilityLayer.GetOs();

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
        var result = CompatibilityLayer.GetExecutablePath(CloudEnvironment.AzureFunction, OS.Windows, new MockEnvironmentVariableProvider());

        Assert.Equal(@"C:\home\site\wwwroot\datadog\bin\windows-amd64\datadog-serverless-compat.exe", result);
    }

    [Fact]
    public void GetExecutablePath_ShouldReturnCorrectPath_ForLinux()
    {
        var result = CompatibilityLayer.GetExecutablePath(CloudEnvironment.AzureFunction, OS.Linux, new MockEnvironmentVariableProvider());

        Assert.Equal("/home/site/wwwroot/datadog/bin/linux-amd64/datadog-serverless-compat", result);
    }

    [Fact]
    public void GetExecutablePath_ShouldReturnOverridePath_WhenDDServerlessCompatPathSet()
    {
        var envVars = new MockEnvironmentVariableProvider();
        envVars.Set("DD_SERVERLESS_COMPAT_PATH", "/custom/path/compat");

        var result = CompatibilityLayer.GetExecutablePath(CloudEnvironment.AzureFunction, OS.Windows, envVars);

        Assert.Equal("/custom/path/compat", result);
    }

    [Fact]
    public void GetPackageVersion_ShouldReturnVersion_WhenAssemblyAttributeExists()
    {
        var result = CompatibilityLayer.GetPackageVersion();

        Assert.NotEqual("unknown", result);
    }

    [Theory]
    [InlineData("FlexConsumption", null, true)]
    [InlineData("FlexConsumption", "test-rg", false)]
    [InlineData("ElasticPremium", null, false)]
    [InlineData("ElasticPremium", "test-rg", false)]
    public void IsAzureFlexWithoutDDAzureResourceGroup_ShouldReturnCorrectValue(string websiteSku, string? ddAzureResourceGroup, bool expected)
    {
        var envVars = new MockEnvironmentVariableProvider();
        envVars.Set("WEBSITE_SKU", websiteSku);
        envVars.Set("DD_AZURE_RESOURCE_GROUP", ddAzureResourceGroup);

        var result = CompatibilityLayer.IsAzureFlexWithoutDDAzureResourceGroup(envVars);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateTracePipeName_ShouldGenerateUniqueName_WhenNoEnvVarSet()
    {
        var pipeName1 = CompatibilityLayer.CalculateTracePipeName(new MockEnvironmentVariableProvider());
        var pipeName2 = CompatibilityLayer.CalculateTracePipeName(new MockEnvironmentVariableProvider());

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
        var envVars = new MockEnvironmentVariableProvider();
        envVars.Set(envVarName, envVarValue);

        var pipeName = CompatibilityLayer.CalculateTracePipeName(envVars);

        Assert.Equal("custom_trace", pipeName);
    }

    [Fact]
    public void CalculateDogStatsDPipeName_ShouldGenerateUniqueName_WhenNoEnvVarSet()
    {
        var pipeName1 = CompatibilityLayer.CalculateDogStatsDPipeName(new MockEnvironmentVariableProvider());
        var pipeName2 = CompatibilityLayer.CalculateDogStatsDPipeName(new MockEnvironmentVariableProvider());

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
        var envVars = new MockEnvironmentVariableProvider();
        envVars.Set(envVarName, envVarValue);

        var pipeName = CompatibilityLayer.CalculateDogStatsDPipeName(envVars);

        Assert.Equal("custom_dogstatsd", pipeName);
    }

    [Fact]
    public void CalculateTracePipeName_ShouldReturnExactName_EvenWhenLong()
    {
        var longPipeName = new string('a', 300);
        var envVars = new MockEnvironmentVariableProvider();
        envVars.Set("DD_TRACE_WINDOWS_PIPE_NAME", longPipeName);

        var pipeName = CompatibilityLayer.CalculateTracePipeName(envVars);

        Assert.Equal(longPipeName, pipeName);
    }

    [Fact]
    public void CalculateDogStatsDPipeName_ShouldReturnExactName_EvenWhenLong()
    {
        var longPipeName = new string('a', 300);
        var envVars = new MockEnvironmentVariableProvider();
        envVars.Set("DD_DOGSTATSD_WINDOWS_PIPE_NAME", longPipeName);

        var pipeName = CompatibilityLayer.CalculateDogStatsDPipeName(envVars);

        Assert.Equal(longPipeName, pipeName);
    }

    // -------------------------------------------------------------------------
    // Instrumentation contract tests
    //
    // The dd-trace-dotnet tracer ships calltarget definitions that target
    // CalculateTracePipeName and CalculateDogStatsDPipeName by exact symbol.
    // Renaming either method, changing its parameters, or moving it to a
    // different type/assembly silently disables the integration — the native
    // profiler skips unrecognised symbols without throwing.
    //
    // These tests fail at compile time (via nameof) on a rename and at runtime
    // on a signature or accessibility change, making the contract explicit.
    // -------------------------------------------------------------------------

    [Fact]
    public void InstrumentationContract_CalculateTracePipeName_SignatureIsStable()
    {
        var method = typeof(CompatibilityLayer).GetMethod(
            nameof(CompatibilityLayer.CalculateTracePipeName),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(string), method!.ReturnType);
        Assert.Empty(method.GetParameters());

        // Assembly name and type name are part of the calltarget contract
        Assert.Equal("Datadog.Serverless.Compat", typeof(CompatibilityLayer).Assembly.GetName().Name);
        Assert.Equal("Datadog.Serverless.CompatibilityLayer", typeof(CompatibilityLayer).FullName);
    }

    [Fact]
    public void InstrumentationContract_CalculateDogStatsDPipeName_SignatureIsStable()
    {
        var method = typeof(CompatibilityLayer).GetMethod(
            nameof(CompatibilityLayer.CalculateDogStatsDPipeName),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(string), method!.ReturnType);
        Assert.Empty(method.GetParameters());
    }
}

/// <summary>
/// ConfigureNamedPipes tests must not run in parallel: the method writes DD_DOGSTATSD_PIPE_NAME
/// to the real process environment so the DogStatsD client SDK can discover it.
/// </summary>
[Collection(nameof(ConfigureNamedPipesTestCollection))]
public class ConfigureNamedPipesTests
{
    [Fact]
    public void ConfigureNamedPipes_ShouldGenerateGuidSuffixedNames_WhenNoEnvVarsSet()
    {
        using var _ = new EnvironmentVariableScope(
            ("DD_TRACE_WINDOWS_PIPE_NAME", null),
            ("DD_TRACE_PIPE_NAME", null),
            ("DD_DOGSTATSD_WINDOWS_PIPE_NAME", null),
            ("DD_DOGSTATSD_PIPE_NAME", null));
        var startInfo = new System.Diagnostics.ProcessStartInfo();

        CompatibilityLayer.ConfigureNamedPipes(startInfo, OS.Windows);

        var resultTracePipeName = startInfo.EnvironmentVariables["DD_APM_WINDOWS_PIPE_NAME"];
        var resultDogstatsdPipeName = startInfo.EnvironmentVariables["DD_DOGSTATSD_WINDOWS_PIPE_NAME"];

        Assert.NotNull(resultTracePipeName);
        Assert.NotNull(resultDogstatsdPipeName);
        Assert.StartsWith("dd_trace_", resultTracePipeName);
        Assert.StartsWith("dd_dogstatsd_", resultDogstatsdPipeName);

        // DD_DOGSTATSD_PIPE_NAME is set for the in-process DogStatsD client SDK.
        // DD_TRACE_PIPE_NAME is intentionally NOT set — the tracer reads its own ExporterSettings,
        // and the mini-agent gets the name via DD_APM_WINDOWS_PIPE_NAME in the spawned process env.
        Assert.Equal(resultDogstatsdPipeName, Environment.GetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME"));
        Assert.Null(Environment.GetEnvironmentVariable("DD_TRACE_PIPE_NAME"));
    }

    [Fact]
    public void ConfigureNamedPipes_ShouldUseExactNames_WhenEnvVarsSet()
    {
        using var _ = new EnvironmentVariableScope(
            ("DD_TRACE_WINDOWS_PIPE_NAME", "custom_trace"),
            ("DD_TRACE_PIPE_NAME", null),
            ("DD_DOGSTATSD_WINDOWS_PIPE_NAME", "custom_dogstatsd"),
            ("DD_DOGSTATSD_PIPE_NAME", null));
        var startInfo = new System.Diagnostics.ProcessStartInfo();

        CompatibilityLayer.ConfigureNamedPipes(startInfo, OS.Windows);

        Assert.Equal("custom_trace", startInfo.EnvironmentVariables["DD_APM_WINDOWS_PIPE_NAME"]);
        Assert.Equal("custom_dogstatsd", startInfo.EnvironmentVariables["DD_DOGSTATSD_WINDOWS_PIPE_NAME"]);

        // Assert — only DogStatsD is set in the current process (for lazy client init)
        Assert.Equal("custom_dogstatsd", Environment.GetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME"));
        Assert.Null(Environment.GetEnvironmentVariable("DD_TRACE_PIPE_NAME"));
    }

    [Fact]
    public void ConfigureNamedPipes_ShouldNotConfigurePipes_OnLinux()
    {
        using var _ = new EnvironmentVariableScope(
            ("DD_TRACE_PIPE_NAME", null),
            ("DD_DOGSTATSD_PIPE_NAME", null));
        var startInfo = new System.Diagnostics.ProcessStartInfo();

        CompatibilityLayer.ConfigureNamedPipes(startInfo, OS.Linux);

        Assert.False(startInfo.EnvironmentVariables.ContainsKey("DD_TRACE_WINDOWS_PIPE_NAME"));
        Assert.False(startInfo.EnvironmentVariables.ContainsKey("DD_DOGSTATSD_WINDOWS_PIPE_NAME"));
        Assert.Null(Environment.GetEnvironmentVariable("DD_TRACE_PIPE_NAME"));
        Assert.Null(Environment.GetEnvironmentVariable("DD_DOGSTATSD_PIPE_NAME"));
    }
}

/// <summary>
/// Helper class to temporarily set environment variables and automatically restore them on disposal.
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

[CollectionDefinition(nameof(ConfigureNamedPipesTestCollection), DisableParallelization = true)]
public class ConfigureNamedPipesTestCollection
{
}
