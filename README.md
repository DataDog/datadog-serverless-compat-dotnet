# Datadog Serverless Compatibility Layer for .NET
## `Datadog.Serverless.Compat`

**Note: If your Azure Functions are running on an App Service plan (aka Dedicated plan) on Windows, use the [Datadog Azure App Services Site Extension](https://docs.datadoghq.com/serverless/azure_app_services/azure_app_services_windows/?tab=net) instead.**

Add this package to your Azure Functions project to enable Datadog APM tracing and custom metric submission.
Further configuration is required for your Azure Functions to send instrumentation data to Datadog.
For more information, see the Datadog documentation at https://docs.datadoghq.com/serverless/azure_functions/

Contact us with questions or feedback, use https://www.datadoghq.com/support/

## Inventory version metadata

With `DD_SERVERLESS_COMPAT_METADATA_ENABLED=true`, inventory reports the wrapper package as
`serverless_compat_runtime_version`, the embedded binary release as
`serverless_compat_binary_version`, and the language engine in `runtime`.
`DD_SERVERLESS_COMPAT_PATH` selects a custom binary but does not change the wrapper package version.
The binary must supply its own embedded version; an unversioned binary must not inherit the wrapper version.

The wrapper passes a combined language and version hint (for example, `python3.13.7`)
through `DD_SERVERLESS_COMPAT_RUNTIME`
to the child binary without overwriting platform-owned version variables. This requires a
binary that supports this contract; older binaries retain their platform-based runtime detection.
