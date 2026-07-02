// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Microsoft.Azure.Azclips.Cmdlets.Login;

/// <summary>
/// PowerShell-specific User-Agent handler that stamps
/// <c>AzurePowershell/v0.1.0</c> on every outgoing ARM request.
/// </summary>
public class PSUserAgentHandler : IUserAgentHandler
{
    private static readonly string s_userAgent = "AzurePowershell/v0.1.0";

    public SendAsyncStep SendAsync => (request, callback, next) =>
    {
        var moduleAgent = GetCallingModuleAgent();
        var ua = moduleAgent is not null ? $"{moduleAgent} {s_userAgent}" : s_userAgent;

        if (!request.Headers.UserAgent.TryParseAdd(ua))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", ua);
        }
        return next.SendAsync(request, callback);
    };

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
}
