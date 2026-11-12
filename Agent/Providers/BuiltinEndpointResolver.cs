// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Agent.Providers;

/// <summary>
/// The model endpoint a builtin-provider session calls, and why it was chosen.
/// </summary>
/// <param name="Url">Base URL handed to the Copilot SDK provider.</param>
/// <param name="Source">Always <see cref="BuiltinEndpointResolver.BoundarySource"/>.</param>
/// <param name="Boundary">
/// The tenant data boundary behind the choice (<c>Unknown</c> when no answer was available).
/// </param>
internal sealed record BuiltinEndpoint(string Url, string Source, string? Boundary)
{
    public string? Host => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.Host : null;
}

/// <summary>
/// Chooses the regional model gateway for the builtin provider.
/// </summary>
/// <remarks>
/// Confirmed Global uses East US; all other values use West Europe.
/// Model routing is independent of telemetry's scenario-based fallback policy.
/// AgentStartupBoundary resolves the boundary with the token used for the model.
/// An override in <see cref="BuiltinProviderOptions.GlobalEndpoint"/> or
/// <see cref="BuiltinProviderOptions.EuEndpoint"/> replaces the chosen region's default when it is
/// an absolute https URL whose host is in that region's allowlist; otherwise the default is used
/// and a warning is returned.
/// </remarks>
internal static class BuiltinEndpointResolver
{
    internal const string ProdEastUs = "https://azclis-copilot-apim-prod-eus.azure-api.net/azclips/openai/";
    internal const string ProdWestEurope = "https://azclis-copilot-apim-prod-weu.azure-api.net/azclips/openai/";

    internal const string BoundarySource = "boundary";

    private const string UnknownBoundary = "Unknown";

    // The provider sends the user's ARM token to this endpoint, so an override may only name
    // a gateway in the same region.
    private static readonly HashSet<string> GlobalHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "azclis-copilot-apim-ppe-eus.azure-api.net",
        "azclis-copilot-apim-prod-eus.azure-api.net",
    };

    private static readonly HashSet<string> EuHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "azclis-copilot-apim-ppe-weu.azure-api.net",
        "azclis-copilot-apim-prod-weu.azure-api.net",
    };

    /// <summary>Chooses the endpoint from a boundary that is already known; null means not known.</summary>
    public static BuiltinEndpoint Choose(string? boundary) =>
        Choose(boundary, options: null, reportWarning: null);

    /// <summary>
    /// Chooses the endpoint from a boundary that is already known; null means not known.
    /// A valid override in <paramref name="options"/> for the chosen region replaces the default;
    /// an invalid one is reported to <paramref name="reportWarning"/>.
    /// </summary>
    public static BuiltinEndpoint Choose(string? boundary, BuiltinProviderOptions? options, Action<string>? reportWarning)
    {
        var global = string.Equals(boundary, "Global", StringComparison.OrdinalIgnoreCase);
        var url = global
            ? Override(options?.GlobalEndpoint, "agent.builtin.globalEndpoint", GlobalHosts, ProdEastUs, reportWarning)
            : Override(options?.EuEndpoint, "agent.builtin.euEndpoint", EuHosts, ProdWestEurope, reportWarning);
        return new BuiltinEndpoint(
            url,
            BoundarySource,
            string.IsNullOrWhiteSpace(boundary) ? UnknownBoundary : boundary);
    }

    private static string Override(
        string? configured, string key, HashSet<string> allowedHosts, string defaultUrl, Action<string>? reportWarning)
    {
        var value = configured?.Trim();
        if (string.IsNullOrEmpty(value))
            return defaultUrl;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && allowedHosts.Contains(uri.Host))
        {
            return value;
        }

        reportWarning?.Invoke(
            $"Ignoring '{key}': it must be an https URL with one of these hosts: " +
            $"{string.Join(", ", allowedHosts.Order(StringComparer.OrdinalIgnoreCase))}. Using the default endpoint.");
        return defaultUrl;
    }
}
