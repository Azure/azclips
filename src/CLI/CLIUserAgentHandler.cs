// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Cli;

/// <summary>
/// CLI-specific User-Agent handler that stamps
/// <c>AZURECLI/{version}</c> on every outgoing ARM request.
/// </summary>
public class CLIUserAgentHandler : IUserAgentHandler
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
        var version = AssemblyVersionProvider.GetVersion(typeof(CLIUserAgentHandler).Assembly, "0.0.0");
        return $"AZURECLI/{version} azclips/public-preview";
    }
}
