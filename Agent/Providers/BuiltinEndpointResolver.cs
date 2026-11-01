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
/// Global boundaries and their normalized ROW destination use East US. EU and unresolved
/// boundaries use West Europe. The caller resolves the boundary once, with the same token
/// it uses for the model (see <c>AgentStartupBoundary</c>).
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
        var url = string.Equals(boundary, "Global", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(boundary, "ROW", StringComparison.OrdinalIgnoreCase)
            ? PpeEastUs
            : PpeWestEurope;
        return new BuiltinEndpoint(
            url,
            BoundarySource,
            string.IsNullOrWhiteSpace(boundary) ? UnknownBoundary : boundary);
    }
}
