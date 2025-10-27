# Datadog Serverless Compatibility Layer for .NET
## `Datadog.Serverless.Compat`

**Note: If your Azure Functions are running on an App Service plan (aka Dedicated plan or Premium plan) on Windows, use the [Datadog Azure App Services Site Extension](https://docs.datadoghq.com/serverless/azure_app_services/azure_app_services_windows/?tab=net) instead.**

Add this package to your Azure Functions project to enable Datadog APM tracing and custom metric submission.
Further configuration is required for your Azure Functions to send instrumentation data to Datadog.
For more information, see the Datadog documentation at https://docs.datadoghq.com/serverless/azure_functions/

Contact us with questions or feedback, use https://www.datadoghq.com/support/

## Development

### Building

```bash
dotnet build Datadog.Serverless.sln
```

### Running Tests

```bash
dotnet test Datadog.Serverless.Compat.Tests/Datadog.Serverless.Compat.Tests.csproj
```

### Building the NuGet Package

```bash
cd Datadog.Serverless
dotnet pack -p:Version=1.0.0 -c Release
```
