namespace Group.Business.Models;

public class CreateGroupRequest
{
    public required string Name { get; init; }
    public required string Location { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
}
