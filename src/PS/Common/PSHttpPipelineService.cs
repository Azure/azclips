// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Runtime;

namespace Microsoft.Azure.Cirrus.Cmdlets.Common;

/// <summary>
/// PowerShell-specific HTTP pipeline service that stamps a User-Agent header
/// on every outgoing ARM request, following the Az PowerShell convention:
/// <c>Azclips.Login/{moduleVersion} AzurePowershell/{productVersion} PSVersion/{psVersion}</c>
/// </summary>
public class PSHttpPipelineService(
    IHttpTerminateHandler httpTerminateHandler,
    ITracingInterceptor tracingInterceptor)
    : HttpPipelineService(httpTerminateHandler, tracingInterceptor)
{
    private static readonly string s_userAgent = BuildUserAgent();

    public override SendAsyncStep UserAgentStep => (request, callback, next) =>
    {
        var moduleAgent = GetCallingModuleAgent();
        var ua = moduleAgent is not null ? $"{moduleAgent} {s_userAgent}" : s_userAgent;

        if (!request.Headers.UserAgent.TryParseAdd(ua))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", ua);
        }
        return next.SendAsync(request, callback);
    };

    private static string BuildUserAgent()
    {
        var productVersion = GetProductVersion();
        var psVersion = GetPSVersion();

        // Base UA without module info — module segment is added per-request
        // when the calling module is known.
        return $"AzurePowershell/v{productVersion} PSVersion/v{psVersion}";
    }

    /// <summary>
    /// Builds the module-specific prefix (e.g. "Az.Accounts/0.1.0") by inspecting the
    /// calling PowerShell module from the current runspace.
    /// </summary>
    private static string? GetCallingModuleAgent()
    {
        try
        {
            using var ps = System.Management.Automation.PowerShell.Create(System.Management.Automation.RunspaceMode.CurrentRunspace);
            var results = ps.AddScript("$ExecutionContext.SessionState.Module | ForEach-Object { \"$($_.Name)/$($_.Version)\" }").Invoke<string>();
            if (results.Count > 0 && !string.IsNullOrEmpty(results[0]))
            {
                return results[0];
            }
        }
        catch
        {
            // Runspace may not be available during initialization.
        }

        return null;
    }

    private static string GetProductVersion()
    {
        var asm = typeof(PSHttpPipelineService).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = info?.InformationalVersion ?? asm.GetName().Version?.ToString() ?? "0.0.0";
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }

    private static string GetPSVersion()
    {
        try
        {
            // $PSVersionTable.PSVersion is available via the PSVersion environment variable
            // set by PowerShell, or we can read it from the hosting runtime.
            var ps = System.Management.Automation.PowerShell.Create();
            var result = ps.AddScript("$PSVersionTable.PSVersion.ToString()").Invoke<string>();
            if (result.Count > 0 && !string.IsNullOrEmpty(result[0]))
            {
                return result[0];
            }
        }
        catch
        {
            // Fallback if PowerShell runspace is unavailable.
        }

        return Environment.Version.ToString();
    }
}
