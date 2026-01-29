using System.Text.Json;

namespace Core.Agent.Standalone;

/// <summary>
/// Parses JSON responses from the agent to extract type and response fields.
/// </summary>
internal static class AgentResponseParser
{
    /// <summary>
    /// Attempts to parse the agent's JSON response and extract the type and response text.
    /// </summary>
    /// <param name="response">The raw response from the agent (may be wrapped in markdown).</param>
    /// <param name="type">The type field from the JSON (e.g., "exit", "error", etc.).</param>
    /// <param name="responseText">The response field containing the message to display.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(string response, out string? type, out string? responseText)
    {
        type = null;
        responseText = null;

        try
        {
            // Extract JSON from response (it might be wrapped in markdown code blocks)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("type", out var typeElement))
                {
                    type = typeElement.GetString();
                }

                if (doc.RootElement.TryGetProperty("response", out var responseElement))
                {
                    responseText = responseElement.GetString();
                }

                return type != null && responseText != null;
            }
        }
        catch
        {
            // Not valid JSON or missing fields
        }

        return false;
    }
}
