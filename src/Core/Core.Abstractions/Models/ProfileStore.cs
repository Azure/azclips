namespace Core.Abstractions;

public class ProfileStore
{
    public int SchemaVersion { get; set; } = 1;
    public string? ActiveProfile { get; set; }
    public Dictionary<string, Profile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
