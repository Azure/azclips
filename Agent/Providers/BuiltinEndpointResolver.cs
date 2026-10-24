// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
/// The tenant's ARM data boundary picks the gateway with the same rule telemetry upload
/// uses (<c>DataBoundaryRouting.IsConfirmedGlobal</c>): only a confirmed <c>Global</c> answer uses
/// East US. EU tenants, failed or timed-out lookups, and managed-identity profiles (which carry no
/// tenant) use West Europe, so an EU tenant's prompts stay in the EU even when the lookup cannot
/// finish. The caller resolves the boundary once, with the same token it uses for the model
/// (see <c>AgentStartupBoundary</c>).
/// </remarks>
internal static class BuiltinEndpointResolver
{
    internal const string PpeEastUs = "https://azclis-copilot-apim-ppe-eus.azure-api.net/azclips/openai/";
    internal const string PpeWestEurope = "https://azclis-copilot-apim-ppe-weu.azure-api.net/azclips/openai/";

    internal const string BoundarySource = "boundary";

    private const string UnknownBoundary = "Unknown";

    /// <summary>Chooses the endpoint from a boundary that is already known; null means not known.</summary>
    public static BuiltinEndpoint Choose(string? boundary)
    {
        var url = string.Equals(boundary, "Global", StringComparison.OrdinalIgnoreCase)
            ? PpeEastUs
            : PpeWestEurope;
        return new BuiltinEndpoint(
            url,
            BoundarySource,
            string.IsNullOrWhiteSpace(boundary) ? UnknownBoundary : boundary);
    }
}
