using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Core.Abstractions;

public partial class Profile
{
    private static readonly Regex NamePattern = ValidNameRegex();

    public required string Name { get; set; }
    public required string TenantId { get; set; }
    public required string SubscriptionId { get; set; }
    public string CloudName { get; set; } = "AzureCloud";
    public required string AuthMethod { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? ClientId { get; set; }
    public Dictionary<string, string> Defaults { get; set; } = new();

    [JsonIgnore]
    public static readonly string[] AllowedAuthMethods = ["browser", "device-code", "managed-identity", "service-principal"];

    public static bool IsValidName(string name) => !string.IsNullOrEmpty(name) && NamePattern.IsMatch(name);

    [GeneratedRegex(@"^[a-zA-Z0-9_-]+$")]
    private static partial Regex ValidNameRegex();
}
