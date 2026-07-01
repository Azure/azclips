// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Core.Cli;

/// <summary>
/// CLI-specific User-Agent handler that stamps
/// <c>AZURECLI/{version} dotnet/{runtime}</c> on every outgoing ARM request.
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
        var parts = new List<string>
        {
            $"AZURECLI/{GetProductVersion()}",
            $"dotnet/{Environment.Version}",
        };

        // Honour AZ_INSTALLER env var (e.g. "PIP", "MSI", "DOCKER") like azure-cli does.
        var installer = Environment.GetEnvironmentVariable("AZ_INSTALLER");
        if (!string.IsNullOrEmpty(installer))
        {
            parts.Add($"({installer})");
        }

        return string.Join(' ', parts);
    }

    private static string GetProductVersion()
    {
        var asm = typeof(CLIUserAgentHandler).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = info?.InformationalVersion ?? asm.GetName().Version?.ToString() ?? "0.0.0";
        // Strip "+commitsha" suffix appended by the .NET SDK SourceRevisionId
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }
}
