using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tests.Common.Utilities;

namespace Tests.Common.Stubs;

/// <summary>
/// Mock HTTP handler that writes each request to a file and returns a canned JSON response.
/// Replaces the real HttpClientHandler via DI so the full pipeline executes without network I/O.
/// </summary>
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly string _requestOutputDir;
    private int _requestCounter;

    public StubHttpMessageHandler(string? requestOutputDir = null)
    {
        _requestOutputDir = requestOutputDir
            ?? Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "Benchmark", "requests");
        Directory.CreateDirectory(_requestOutputDir);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Write request to numbered JSON file
        var counter = Interlocked.Increment(ref _requestCounter);
        await WriteRequestAsync(request, counter);

        // Select canned response based on URL pattern
        var url = request.RequestUri?.AbsolutePath ?? "";
        var responseBody = SelectResponse(request.Method, url);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }

    private async Task WriteRequestAsync(HttpRequestMessage request, int counter)
    {
        try
        {
            var requestData = new
            {
                Method = request.Method.ToString(),
                Url = request.RequestUri?.ToString(),
                Headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                Body = request.Content != null ? await request.Content.ReadAsStringAsync() : null
            };

            var json = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { WriteIndented = true });
            var filePath = Path.Combine(_requestOutputDir, $"request-{counter:D3}.json");
            await File.WriteAllTextAsync(filePath, json);
        }
        catch
        {
            // Best effort — don't fail the benchmark if request logging fails
        }
    }

    private static string SelectResponse(HttpMethod method, string url)
    {
        // PUT .../resourcegroups/{name} → create
        if (method == HttpMethod.Put && Regex.IsMatch(url, @"/resourcegroups/[^/]+$", RegexOptions.IgnoreCase))
        {
            var name = url.Split('/').Last();
            return MockResponses.ResourceGroupCreate
                .Replace("{name}", name)
                .Replace("{location}", "eastus");
        }

        // GET .../resourcegroups/{name} → get single
        if (method == HttpMethod.Get && Regex.IsMatch(url, @"/resourcegroups/[^/?]+$", RegexOptions.IgnoreCase))
        {
            var name = url.Split('/').Last();
            return MockResponses.ResourceGroupGet.Replace("{name}", name);
        }

        // GET .../resourcegroups (no specific name) → list
        if (method == HttpMethod.Get && Regex.IsMatch(url, @"/resourcegroups(\?|$)", RegexOptions.IgnoreCase))
        {
            return MockResponses.ResourceGroupList;
        }

        // Unmatched → 501 body (status is still 200 to avoid error handling in pipeline)
        return """{"error": {"code": "NotImplemented", "message": "No canned response for this request"}}""";
    }
}
