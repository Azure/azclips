using System.Text.Json.Serialization;

namespace Core.Abstractions;

/// <summary>
/// Represents a named connection profile containing identity context, cloud environment,
/// and extensible default parameters. Profiles persist identity context, not credentials/tokens.
/// </summary>
public class Profile
{
    private string _name = "";

    /// <summary>
    /// Unique identifier for the profile (case-insensitive comparison).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set
        {
            ValidateName(value);
            _name = value;
        }
    }

    /// <summary>Microsoft Entra tenant identifier.</summary>
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = "";

    /// <summary>Azure subscription identifier.</summary>
    [JsonPropertyName("subscriptionId")]
    public string SubscriptionId { get; set; } = "";

    /// <summary>Cloud environment name (e.g., AzureCloud, AzureChinaCloud, AzureUSGovernment).</summary>
    [JsonPropertyName("cloudName")]
    public string CloudName { get; set; } = "AzureCloud";

    /// <summary>Authentication method used (e.g., "interactive", "device-code", "managed-identity", "service-principal").</summary>
    [JsonPropertyName("authMethod")]
    public string? AuthMethod { get; set; }

    /// <summary>User principal name of the authenticated user.</summary>
    [JsonPropertyName("userPrincipalName")]
    public string? UserPrincipalName { get; set; }

    /// <summary>Application/client ID for service principal authentication.</summary>
    [JsonPropertyName("clientId")]
    public string? ClientId { get; set; }

    /// <summary>
    /// Extensible key-value pairs for default parameters (e.g., "location", "resource-group").
    /// </summary>
    [JsonPropertyName("defaults")]
    public Dictionary<string, string> Defaults { get; set; } = new();

    /// <summary>
    /// Validates that a profile name is non-empty, not whitespace-only, and contains no path separators.
    /// </summary>
    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name must not be empty or whitespace-only.", nameof(name));
        if (name.Contains('/') || name.Contains('\\'))
            throw new ArgumentException($"Profile name must not contain path separators ('/' or '\\'). Got: '{name}'", nameof(name));
    }

    public override string ToString()
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Name: {Name}");
        lines.AppendLine($"Tenant: {TenantId}");
        lines.AppendLine($"Subscription: {SubscriptionId}");
        lines.AppendLine($"Cloud: {CloudName}");
        if (!string.IsNullOrEmpty(AuthMethod)) lines.AppendLine($"Auth Method: {AuthMethod}");
        if (!string.IsNullOrEmpty(UserPrincipalName)) lines.AppendLine($"User: {UserPrincipalName}");
        if (!string.IsNullOrEmpty(ClientId)) lines.AppendLine($"Client ID: {ClientId}");
        if (Defaults.Count > 0)
        {
            lines.AppendLine("Defaults:");
            foreach (var kvp in Defaults)
                lines.AppendLine($"  {kvp.Key}: {kvp.Value}");
        }
        return lines.ToString().TrimEnd();
    }
}
