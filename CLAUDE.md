# CLAUDE.md

AI agent guidance for working with this repository.

## Repository Identity

**Name**: `datadog-serverless-compat-dotnet`
**Package**: `Datadog.Serverless.Compat` NuGet package
**Purpose**: Compatibility layer enabling Datadog APM tracing and custom metrics in Azure Functions
**Status**: Unsupported pre-release
**Exclusion**: NOT for Azure Functions on App Service (Dedicated) plans on Windows → use Datadog Azure App Services Site Extension instead

## File Locations Map

```
Datadog.Serverless/
├── Datadog.Serverless.Compat.csproj    # Main package (net6.0, net461)
├── StartupHook.cs                       # .NET startup hook entry point
├── CompatibilityLayer.cs                # Core logic (agent spawning)
└── datadog/bin/                         # Binary artifacts (CI-generated)
    ├── windows-amd64/
    │   └── datadog-serverless-compat.exe
    └── linux-amd64/
        └── datadog-serverless-compat

Datadog.Serverless.Compat.Tests/
├── Datadog.Serverless.Compat.Tests.csproj  # Tests (net8.0, net48)
└── *.cs                                     # xUnit test files

SendEmptyTrace/
└── Program.cs                           # Test utility: sends MessagePack traces to localhost:8126

.github/workflows/
├── unit-tests.yaml                      # CI: runs on push/PR to main
└── publish.yaml                         # Manual: builds/publishes NuGet package
```

## Common Commands

```bash
# Build entire solution
dotnet build Datadog.Serverless.sln

# Run all tests
dotnet test Datadog.Serverless.Compat.Tests/Datadog.Serverless.Compat.Tests.csproj

# Run specific test by name filter
dotnet test Datadog.Serverless.Compat.Tests/Datadog.Serverless.Compat.Tests.csproj --filter "FullyQualifiedName~TestName"

# Build NuGet package locally (requires binary in datadog/bin/)
cd Datadog.Serverless
dotnet pack -p:Version=1.0.0 -c Release
```

## Architecture Quick Reference

### Startup Hook Mechanism
- **Entry**: `StartupHook.cs` invoked by .NET runtime BEFORE `Main()`
- **Flow**: `StartupHook.Initialize()` → `CompatibilityLayer.Start()`
- **Docs**: https://github.com/dotnet/runtime/blob/main/docs/design/features/host-startup-hook.md

### CompatibilityLayer.Start() Execution Flow
1. Detect OS (Windows/Linux) and cloud environment (Azure Functions)
2. Validate: Azure Flex Consumption requires `DD_AZURE_RESOURCE_GROUP` env var (exit early if missing)
3. Locate binary: check `DD_SERVERLESS_COMPAT_PATH` override → fallback to default paths
4. Platform-specific setup:
   - **Linux**: copy binary to temp dir, `chmod 0744`, execute from temp
   - **Windows**: execute binary in place
5. Spawn `datadog-serverless-compat` as background process

### Environment Variables
| Variable | Purpose | Required |
|----------|---------|----------|
| `DD_SERVERLESS_COMPAT_PATH` | Override default binary path | No |
| `DD_AZURE_RESOURCE_GROUP` | Azure resource group (required for Flex Consumption) | Conditional |
| `DD_LOG_LEVEL` | Logging verbosity (DEBUG, INFO, WARNING, ERROR) | No |

### Default Binary Paths
- **Windows**: `C:\home\site\wwwroot\datadog\bin\windows-amd64\datadog-serverless-compat.exe`
- **Linux**: `/home/site/wwwroot/datadog/bin/linux-amd64/datadog-serverless-compat`

## CI/CD Pipelines

### unit-tests.yaml
- **Trigger**: Push/PR to `main`
- **Matrix**: `ubuntu-latest`, `windows-latest`
- **SDK**: .NET 9.0
- **Steps**: checkout → restore → build → test

### publish.yaml
- **Trigger**: Manual workflow dispatch
- **Binary Source**: https://github.com/DataDog/serverless-components/releases
  - Queries GitHub API for latest `datadog-serverless-compat/v*` release
  - Downloads `datadog-serverless-compat.zip`
  - Extracts to `Datadog.Serverless/datadog/bin/`
- **Version**: From git tag `v*` or manual override input
- **Artifacts**: `.nupkg` (package) + `.snupkg` (symbols)
- **Options**: Publish to NuGet, create GitHub release (both optional)
- **Build Config**: Deterministic builds (`ContinuousIntegrationBuild=true`)

## Code Modification Guidelines

### When Editing CompatibilityLayer.cs
- Preserve OS-specific logic (Windows vs Linux paths/permissions)
- Maintain early-exit for Azure Flex Consumption without `DD_AZURE_RESOURCE_GROUP`
- Update validation logic if adding new environment requirements
- Test on both Windows and Linux (CI matrix covers this)

### When Editing StartupHook.cs
- Minimal code only (called before Main())
- Avoid exceptions that could crash host process
- Log errors via `CompatibilityLayer` logging mechanism

### When Adding Tests
- Add to `Datadog.Serverless.Compat.Tests/`
- Use xUnit framework
- Target `net8.0` (and `net48` on Windows)
- Verify tests run on both `ubuntu-latest` and `windows-latest`

### When Modifying CI
- **unit-tests.yaml**: Changes affect every PR → test thoroughly
- **publish.yaml**: Validate version parsing logic, test with manual dispatch
- Binary download logic: verify `serverless-components` release tag pattern

## PR Requirements

Use template at `.github/pull_request_template.md`:
- What does this PR do?
- Motivation
- Additional Notes
- Describe how to test/QA your changes

## External Dependencies

- **Binary**: https://github.com/DataDog/serverless-components (Rust codebase)
- **Deployment Target**: Azure Functions (Flex Consumption, Linux Consumption)
- **Runtime**: .NET startup hooks feature (net6.0+, net461)
