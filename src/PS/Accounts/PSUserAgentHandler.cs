// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

/// <summary>
/// PowerShell-specific User-Agent handler that stamps
/// <c>AzurePowershell/{version} azclips/private-preview</c> on every outgoing ARM request.
/// </summary>
public class PSUserAgentHandler : IUserAgentHandler
{
    private static readonly string s_userAgent = BuildUserAgent();

    public SendAsyncStep SendAsync => (request, callback, next) =>
    {
        if (!request.Headers.UserAgent.TryParseAdd(s_userAgent))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", s_userAgent);
        }
        return next.SendAsync(request, callback);
    };

    private static string BuildUserAgent()
    {
        var version = AssemblyVersionProvider.GetVersion(typeof(PSUserAgentHandler).Assembly, "0.0.0");
        return $"AzurePowershell/{version} azclips/public-preview";
    }
}
