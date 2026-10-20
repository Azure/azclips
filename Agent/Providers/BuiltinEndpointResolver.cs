// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Telemetry;

namespace Core.Agent.Providers;

/// <summary>
/// The model endpoint a builtin-provider session calls, and why it was chosen.
/// </summary>
/// <param name="Url">Base URL handed to the Copilot SDK provider.</param>
/// <param name="Source">
/// <see cref="BuiltinEndpointResolver.ConfiguredSource"/> when <c>agent.builtin.endpoint</c> is set;
/// otherwise <see cref="BuiltinEndpointResolver.BoundarySource"/>.
/// </param>
/// <param name="Boundary">
/// The tenant data boundary behind a boundary-based choice (<c>Unknown</c> when no answer was
/// available); null for a configured endpoint.
/// </param>
internal sealed record BuiltinEndpoint(string Url, string Source, string? Boundary)
{
    public string? Host => Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.Host : null;
}

/// <summary>
/// Chooses the regional model gateway for the builtin provider.
/// </summary>
/// <remarks>
/// An explicitly configured <c>agent.builtin.endpoint</c> is used as-is. When it is empty (the
/// default), the tenant's ARM data boundary picks the gateway with the same rule telemetry upload
/// uses (<c>DataBoundaryRouting.IsConfirmedGlobal</c>): only a confirmed <c>Global</c> answer uses
/// East US. EU tenants, failed or timed-out lookups, and managed-identity profiles (which carry no
/// tenant) use West Europe, so an EU tenant's prompts stay in the EU even when the lookup cannot
/// finish. The lookup reuses <see cref="TelemetryRoutingResolver"/>: it reads the per-tenant
/// boundary file cache (the CLI's startup telemetry-routing pass fills it) and makes one ARM
/// call, bounded by <see cref="TelemetryRoutingResolver.DefaultTimeout"/>, only on a cache miss.
/// </remarks>
internal static class BuiltinEndpointResolver
{
    internal const string PpeEastUs = "https://azclis-copilot-apim-ppe-eus.azure-api.net/azclips/openai/";
    internal const string PpeWestEurope = "https://azclis-copilot-apim-ppe-weu.azure-api.net/azclips/openai/";

    internal const string ConfiguredSource = "configured";
    internal const string BoundarySource = "boundary";

    private const string UnknownBoundary = "Unknown";

    public static Task<BuiltinEndpoint> ResolveAsync(
        BuiltinProviderOptions opts,
        IProfileService profileService,
        IAuthService auth,
        CancellationToken ct) =>
        ResolveAsync(
            opts,
            async token =>
            {
                var routing = await TelemetryRoutingResolver.ResolveAsync(
                    profileService, auth, profileNameOverride: null, token).ConfigureAwait(false);
                return routing.Boundary;
            },
            ct);

    internal static async Task<BuiltinEndpoint> ResolveAsync(
        BuiltinProviderOptions opts,
        Func<CancellationToken, Task<string?>> lookupBoundary,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(opts.Endpoint))
            return new BuiltinEndpoint(opts.Endpoint, ConfiguredSource, Boundary: null);

        string? boundary;
        try
        {
            boundary = await lookupBoundary(ct).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            boundary = null;
        }

        // TelemetryRoutingResolver returns null on any failure, including caller cancellation.
        ct.ThrowIfCancellationRequested();
        return Choose(opts, boundary);
    }

    /// <summary>Chooses the endpoint from a boundary that is already known; null means not known.</summary>
    public static BuiltinEndpoint Choose(BuiltinProviderOptions opts, string? boundary)
    {
        if (!string.IsNullOrWhiteSpace(opts.Endpoint))
            return new BuiltinEndpoint(opts.Endpoint, ConfiguredSource, Boundary: null);

        var url = string.Equals(boundary, "Global", StringComparison.OrdinalIgnoreCase)
            ? PpeEastUs
            : PpeWestEurope;
        return new BuiltinEndpoint(
            url,
            BoundarySource,
            string.IsNullOrWhiteSpace(boundary) ? UnknownBoundary : boundary);
    }
}
