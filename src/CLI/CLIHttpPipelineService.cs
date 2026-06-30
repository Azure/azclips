// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Runtime;

namespace Core.Cli;

/// <summary>
/// CLI-specific HTTP pipeline service that stamps a User-Agent header
/// on every outgoing ARM request, following the azure-cli convention:
/// <c>AZURECLI/{version} ({platform}) dotnet/{runtime}</c>
/// </summary>
public class CLIHttpPipelineService(
    IHttpTerminateHandler httpTerminateHandler,
    ITracingInterceptor tracingInterceptor)
    : HttpPipelineService(httpTerminateHandler, tracingInterceptor)
{
    private static readonly string s_userAgent = BuildUserAgent();

    public override SendAsyncStep UserAgentStep => (request, callback, next) =>
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
        var asm = typeof(CLIHttpPipelineService).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = info?.InformationalVersion ?? asm.GetName().Version?.ToString() ?? "0.0.0";
        // Strip "+commitsha" suffix appended by the .NET SDK SourceRevisionId
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }
}
