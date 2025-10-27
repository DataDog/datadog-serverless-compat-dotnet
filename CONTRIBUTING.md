# Contributing to Datadog Serverless Compatibility Layer for .NET

Welcome! This guide will help you get started contributing to the `Datadog.Serverless.Compat` package.

## Table of Contents

- [Getting Started](#getting-started)
- [Development Environment](#development-environment)
- [Project Architecture](#project-architecture)
- [Development Workflow](#development-workflow)
- [Testing](#testing)
- [Building the Package](#building-the-package)
- [CI/CD Pipelines](#cicd-pipelines)
- [Debugging](#debugging)
- [Release Process](#release-process)
- [Common Issues](#common-issues)

## Getting Started

### Prerequisites

- .NET SDK 9.0 or later
- Git
- A code editor (VS Code, Visual Studio, Rider, etc.)
- (Optional) Azure Functions Core Tools for local testing

### Clone and Build

```bash
git clone https://github.com/DataDog/datadog-serverless-compat-dotnet.git
cd datadog-serverless-compat-dotnet
dotnet build Datadog.Serverless.sln
```

### Run Tests

```bash
dotnet test Datadog.Serverless.Compat.Tests/Datadog.Serverless.Compat.Tests.csproj
```

## Development Environment

### Repository Structure

```
datadog-serverless-compat-dotnet/
├── Datadog.Serverless/              # Main NuGet package
│   ├── Datadog.Serverless.Compat.csproj
│   ├── StartupHook.cs               # .NET startup hook entry point
│   ├── CompatibilityLayer.cs        # Core agent spawning logic
│   └── datadog/bin/                 # Binary artifacts (not in git, CI-generated)
├── Datadog.Serverless.Compat.Tests/ # Unit tests
│   ├── Datadog.Serverless.Compat.Tests.csproj
│   └── *.cs                         # xUnit test files
├── SendEmptyTrace/                  # Test utility
│   └── Program.cs                   # Sends test traces to localhost:8126
├── .github/workflows/
│   ├── unit-tests.yaml              # CI tests on push/PR
│   └── publish.yaml                 # Package publishing workflow
├── CLAUDE.md                        # AI agent guidance
├── CONTRIBUTING.md                  # This file
└── README.md                        # User-facing documentation
```

### Target Frameworks

- **Datadog.Serverless**: Multi-targets `net6.0` and `net461`
- **Tests**: `net8.0` (and `net48` on Windows)
- **SendEmptyTrace**: `net8.0`

## Project Architecture

### How It Works

The package enables Datadog APM tracing in Azure Functions by:

1. **Startup Hook**: Uses [.NET's startup hook feature](https://github.com/dotnet/runtime/blob/main/docs/design/features/host-startup-hook.md) to run code before the Azure Function starts
2. **Agent Spawning**: Spawns a background `datadog-serverless-compat` agent process that receives traces and metrics
3. **Compatibility Layer**: Handles OS-specific differences (Windows vs Linux) and environment detection

### Key Files

#### StartupHook.cs
- Entry point invoked by .NET runtime **before** `Main()`
- Calls `CompatibilityLayer.Start()` to initialize
- Must be minimal and avoid exceptions

#### CompatibilityLayer.cs
- Detects OS (Windows/Linux) and cloud environment (Azure Functions)
- Validates environment variables (e.g., `DD_AZURE_RESOURCE_GROUP` for Flex Consumption)
- Spawns the `datadog-serverless-compat` agent process
- Handles platform-specific concerns:
  - **Linux**: Copies binary to temp dir, sets execute permissions (`chmod 0744`)
  - **Windows**: Executes binary in place

### Environment Variables

| Variable | Purpose | Required |
|----------|---------|----------|
| `DD_SERVERLESS_COMPAT_PATH` | Override default binary path | No |
| `DD_AZURE_RESOURCE_GROUP` | Azure resource group (required for Flex Consumption) | Conditional |
| `DD_LOG_LEVEL` | Logging verbosity (DEBUG, INFO, WARNING, ERROR) | No |

### Default Binary Paths

- **Windows**: `C:\home\site\wwwroot\datadog\bin\windows-amd64\datadog-serverless-compat.exe`
- **Linux**: `/home/site/wwwroot/datadog\bin/linux-amd64/datadog-serverless-compat`

## Development Workflow

### Making Changes

1. **Create a branch** from `main`:
   ```bash
   git checkout -b your-feature-branch
   ```

2. **Make your changes** in the appropriate files

3. **Add/update tests** in `Datadog.Serverless.Compat.Tests/`

4. **Run tests locally**:
   ```bash
   dotnet test Datadog.Serverless.Compat.Tests/Datadog.Serverless.Compat.Tests.csproj
   ```

5. **Build the solution** to verify:
   ```bash
   dotnet build Datadog.Serverless.sln
   ```

6. **Commit your changes** with a clear commit message

7. **Push and create a PR** using the template in `.github/pull_request_template.md`

### PR Template Sections

Your PR should include:
- **What does this PR do?** - Brief description of changes
- **Motivation** - Why is this change needed?
- **Additional Notes** - Any caveats, limitations, or context
- **Describe how to test/QA your changes** - Testing instructions

## Testing

### Unit Tests

We use xUnit for unit tests. Tests are located in `Datadog.Serverless.Compat.Tests/`.

```bash
# Run all tests
dotnet test Datadog.Serverless.Compat.Tests/Datadog.Serverless.Compat.Tests.csproj

# Run specific test by name
dotnet test Datadog.Serverless.Compat.Tests/Datadog.Serverless.Compat.Tests.csproj --filter "FullyQualifiedName~TestName"

# Run with verbose output
dotnet test Datadog.Serverless.Compat.Tests/Datadog.Serverless.Compat.Tests.csproj --verbosity detailed
```

### Testing Guidelines

- Write tests for all new functionality
- Test both Windows and Linux code paths where applicable
- Use meaningful test names that describe what's being tested
- Mock external dependencies (filesystem, processes, etc.)
- Aim for high code coverage of core logic

### Manual Testing with SendEmptyTrace

The `SendEmptyTrace` utility can test agent connectivity:

```bash
# Run the agent locally first (if you have the binary)
# Then run:
dotnet run --project SendEmptyTrace/SendEmptyTrace.csproj
```

This sends an empty MessagePack trace to `http://localhost:8126` to verify the agent is receiving data.

## Building the Package

### Local NuGet Package Build

```bash
cd Datadog.Serverless
dotnet pack -p:Version=1.0.0-local -c Release
```

**Note**: The package expects the `datadog-serverless-compat` binary to exist in `datadog/bin/`. In CI, this is downloaded automatically. For local development, you won't have the binary unless you manually download it from [serverless-components releases](https://github.com/DataDog/serverless-components/releases).

### Binary Dependency

The `datadog-serverless-compat` binary comes from the [DataDog/serverless-components](https://github.com/DataDog/serverless-components) repository:

- Written in Rust
- Separate release cycle from this .NET package
- Downloaded during CI publish workflow
- Contains both Windows and Linux binaries

## CI/CD Pipelines

### unit-tests.yaml

Runs automatically on every push and PR to `main`:

- **Matrix**: Tests on both `ubuntu-latest` and `windows-latest`
- **Steps**: Checkout → Restore → Build → Test
- **SDK**: .NET 9.0

### publish.yaml

Manual workflow for creating releases:

1. **Download Binaries**: Fetches latest `datadog-serverless-compat` from serverless-components
2. **Build Package**: Creates `.nupkg` and `.snupkg` (symbols)
3. **Publish to NuGet** (optional): Requires NuGet API key
4. **Create GitHub Release** (optional): Creates draft release with notes

**Triggering**: Workflow dispatch with options:
- Publish to NuGet (checkbox)
- Publish GitHub release (checkbox)
- Package version override (optional text input)

## Debugging

### Common Debugging Scenarios

#### Debug Startup Hook Initialization

Since the startup hook runs before `Main()`, debugging can be tricky:

1. Add diagnostic logging to `CompatibilityLayer.cs`
2. Set `DD_LOG_LEVEL=DEBUG` in your environment
3. Check Azure Function logs for startup messages

#### Debug Agent Process Spawning

To verify the agent process is starting:

1. Check process list after Function starts
2. Look for `datadog-serverless-compat` process
3. Check agent logs (if available)
4. Verify binary exists at expected path

#### Debug on Linux vs Windows

- Tests run on both platforms in CI
- Use CI logs to debug platform-specific issues
- Pay attention to path separators and permissions

### Logging

The package uses console logging that integrates with Azure Functions logging:

```csharp
Console.WriteLine($"[Datadog.Serverless.Compat] Your message here");
```

Respect the `DD_LOG_LEVEL` environment variable when adding new logging.

## Release Process

### Creating a New Release

1. **Prepare changes**: Ensure all PRs are merged to `main`

2. **Create and push a tag** (or use manual version override):
   ```bash
   git tag v1.2.3
   git push origin v1.2.3
   ```

3. **Run publish workflow**:
   - Go to Actions → "Publish packages on Nuget"
   - Click "Run workflow"
   - Choose options:
     - ✅ Publish to NuGet (if ready for release)
     - ✅ Publish GitHub Release (if ready for release)
     - Version override: Leave blank to use tag, or enter `x.y.z`

4. **Review GitHub release**: The workflow creates a **draft** release—review and publish it

5. **Verify NuGet**: Check that the package appears on https://www.nuget.org/packages/Datadog.Serverless.Compat/

### Version Strategy

- Follow [Semantic Versioning](https://semver.org/)
- Pre-release packages: `x.y.z-alpha`, `x.y.z-beta`, etc.
- Production releases: `x.y.z`

### Deterministic Builds

CI uses deterministic builds (`ContinuousIntegrationBuild=true`) to ensure reproducible packages.

## Common Issues

### Issue: Tests fail on Windows but pass on Linux (or vice versa)

**Solution**: Check for platform-specific code:
- Path separators (use `Path.Combine()`)
- File permissions (Linux only)
- Environment variables (case sensitivity)

### Issue: NuGet package is missing the binary

**Solution**: The binary is downloaded during the `publish.yaml` workflow, not stored in git. For local development, you don't need the binary to build the code, only to test the final package.

### Issue: Startup hook not running in Azure Functions

**Possible causes**:
- Package not referenced correctly
- .NET runtime version mismatch
- Azure Functions runtime issue

**Debugging**:
- Check Function logs for initialization messages
- Verify package is in deployed artifacts
- Ensure target framework compatibility

### Issue: Agent process fails to start

**Possible causes**:
- Binary missing or wrong path
- Permissions issue (Linux: not executable)
- Missing `DD_AZURE_RESOURCE_GROUP` for Flex Consumption

**Debugging**:
- Check logs for specific error messages
- Verify binary exists at expected path
- Check environment variables

## Getting Help

- **Issues**: [GitHub Issues](https://github.com/DataDog/datadog-serverless-compat-dotnet/issues)
- **Datadog Support**: https://www.datadoghq.com/support/
- **Documentation**: https://docs.datadoghq.com/serverless/azure_functions/

## License

See the LICENSE file in the repository root.
