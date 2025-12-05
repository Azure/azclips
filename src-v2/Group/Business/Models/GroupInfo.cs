namespace Group.Business.Models;

public class GroupInfo
{
    public required string Name { get; init; }
    public required string Id { get; init; }
    public required string Location { get; init; }
    public required string ProvisioningState { get; init; }
    public string? ManagedBy { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new();
}
