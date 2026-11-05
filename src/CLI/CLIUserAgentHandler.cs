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
    private readonly string _userAgent;

    public CLIUserAgentHandler(IVersionProvider versionProvider)
    {
        _userAgent = $"AZURECLI/{versionProvider.ProductVersion} azclips/public-preview";
    }

    public SendAsyncStep SendAsync => (request, callback, next) =>
    {
        if (!request.Headers.UserAgent.TryParseAdd(_userAgent))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        }
        return next.SendAsync(request, callback);
    };

}
