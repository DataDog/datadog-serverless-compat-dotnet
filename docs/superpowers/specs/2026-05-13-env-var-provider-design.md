# Design: IEnvironmentVariableProvider Refactor

## Goal

Replace direct `Environment.GetEnvironmentVariable` calls with an injected `IEnvironmentVariableProvider`, enabling true test isolation and parallel test execution.

## New Types

### Production (`Datadog.Serverless/IEnvironmentVariableProvider.cs`)

```csharp
internal interface IEnvironmentVariableProvider
{
    string? GetEnvironmentVariable(string key);
}

internal readonly struct EnvironmentVariableProvider : IEnvironmentVariableProvider
{
    public string? GetEnvironmentVariable(string key)
        => Environment.GetEnvironmentVariable(key);
}
```

### Test (`Datadog.Serverless.Compat.Tests/MockEnvironmentVariableProvider.cs`)

```csharp
internal class MockEnvironmentVariableProvider : IEnvironmentVariableProvider
{
    private readonly Dictionary<string, string> _variables = new();

    public void Set(string key, string? value)
    {
        if (value is null) _variables.Remove(key);
        else _variables[key] = value;
    }

    public string? GetEnvironmentVariable(string key)
        => _variables.TryGetValue(key, out var v) ? v : null;
}
```

## Injection Strategy

**Approach: method-parameter injection** — `IEnvironmentVariableProvider` is passed as a parameter to each static method that reads env vars. Production call sites pass `new EnvironmentVariableProvider()`. Tests pass a `MockEnvironmentVariableProvider` instance.

## Instrumentation Contract Constraint

`CalculateTracePipeName()` and `CalculateDogStatsDPipeName()` have fixed public signatures (the dd-trace-dotnet tracer instruments them by symbol). These keep their no-arg signatures and delegate to new `internal` overloads that accept a provider:

```csharp
public static string CalculateTracePipeName()
    => CalculateTracePipeName(new EnvironmentVariableProvider());

internal static string CalculateTracePipeName(IEnvironmentVariableProvider envVars) { ... }
```

## Affected Methods

### `CompatibilityLayer.cs`
- `GetEnvironment()` → `GetEnvironment(IEnvironmentVariableProvider envVars)`
- `GetExecutablePath(env, os)` → `GetExecutablePath(env, os, IEnvironmentVariableProvider envVars)`
- `IsAzureFlexWithoutDDAzureResourceGroup()` → add provider param
- `DeterminePipeBaseName(windowsKey, key)` → add provider param
- `CalculateTracePipeName()` → keep public signature; add internal overload with provider
- `CalculateDogStatsDPipeName()` → keep public signature; add internal overload with provider
- `ConfigureNamedPipes(startInfo, os)` → add provider param; call internal overloads
- `Start()` → creates `new EnvironmentVariableProvider()`, threads through all calls above

### `Logger.cs`
- `GetLogLevelFromEnvironment()` → `GetLogLevelFromEnvironment(IEnvironmentVariableProvider envVars)`
- Static constructor in `CompatibilityLayer` passes `new EnvironmentVariableProvider()`

## Test Changes

- Add `MockEnvironmentVariableProvider` to test project
- Rewrite all tests using `EnvironmentVariableScope` to use `MockEnvironmentVariableProvider` instead
- Remove `EnvironmentVariableScope` class
- Remove `[CollectionDefinition(..., DisableParallelization = true)]` and `[Collection(...)]` attributes
- Add tests for `Logger.GetLogLevelFromEnvironment` using mock provider
