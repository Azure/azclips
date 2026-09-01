using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;

namespace Core.Authentication;

/// <summary>
/// Fetches the ARM data boundary for the authenticated tenant by calling
/// <c>GET /providers/Microsoft.Resources/dataBoundaries?api-version=2023-07-01</c>.
/// </summary>
public class DataBoundaryService(HttpClient httpClient, ILoggingService logger) : IDataBoundaryService
{
    private const string ArmBaseUrl = "https://management.azure.com";
    private const string ApiVersion = "2023-07-01";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<string?> GetDataBoundaryAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var uri = $"{ArmBaseUrl}/providers/Microsoft.Resources/dataBoundaries/default?api-version={ApiVersion}";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<DataBoundaryResponse>(body, s_jsonOptions);

            var boundary = result?.Properties?.DataBoundary;
            if (!string.IsNullOrEmpty(boundary))
            {
                logger.Debug($"[{nameof(DataBoundaryService)}] Resolved data boundary: '{boundary}'.");
                return boundary;
            }
        }
        catch (Exception ex)
        {
            logger.Debug($"[{nameof(DataBoundaryService)}] Failed to fetch data boundary: {ex.Message}");
        }

        return null;
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
