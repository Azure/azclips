// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Text.Json;

namespace Core.Telemetry;

/// <summary>
/// Fetches the ARM data boundary for a tenant by calling
/// <c>GET /providers/Microsoft.Resources/dataBoundaries/default?api-version=2023-07-01</c>.
/// Internal implementation detail of the telemetry layer — not exposed publicly.
/// </summary>
internal static class DataBoundaryService
{
    private const string ArmBaseUrl = "https://management.azure.com";
    private const string ApiVersion = "2023-07-01";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Queries the ARM dataBoundaries API and returns the boundary value (e.g. "EU", "Global"),
    /// or <see langword="null"/> when the API is unavailable or returns no value.
    /// </summary>
    internal static async Task<string?> GetDataBoundaryAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var uri = $"{ArmBaseUrl}/providers/Microsoft.Resources/dataBoundaries/default?api-version={ApiVersion}";
        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<DataBoundaryResponse>(body, s_jsonOptions);

            return result?.Properties?.DataBoundary;
        }
        catch
        {
            return null;
        }
    }

    private sealed class DataBoundaryResponse
    {
        public string? Name { get; set; }
        public DataBoundaryProperties? Properties { get; set; }
    }

    private sealed class DataBoundaryProperties
    {
        public string? DataBoundary { get; set; }
        public string? ProvisioningState { get; set; }
    }
}
