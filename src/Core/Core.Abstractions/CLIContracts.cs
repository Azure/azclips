namespace Core.Abstractions;

/// <summary>
/// Interface that all command processors must implement.
/// Processors receive their dependencies via constructor injection (ActivatorUtilities)
/// and their CLI/PS parameters via property binding after construction.
/// </summary>
public interface IProcessor
{
    Task ProcessAsync();
}

public enum ArgumentAction
{
    Store,
    // StoreTrue,
    // Count,
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ArgumentInfoAttribute : Attribute
{
    public bool Required { get; set; } = false;
    public string Description { get; set; } = "";
    public string[] Enums { get; set; } = Array.Empty<string>();
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public ArgumentAction Action { get; set; } = ArgumentAction.Store;
    /// <summary>
    /// Indicates if the property is positional in the command line arguments.
    /// Each command could have one positional argument.
    /// </summary>
    // public bool Positional { get; set; } = false;
    // public string NumArgs { get; set; } = "1"; // Default to 1, can be set to '*' for variable length
    // public string? Group { get; set; } = null;
    public string SerializedName { get; set; } = "";
}

[AttributeUsage(AttributeTargets.Class)]
public class CommandInfoAttribute : Attribute
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
