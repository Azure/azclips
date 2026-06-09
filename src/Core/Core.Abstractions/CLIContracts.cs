using Core.Abstractions.Runtime;

namespace Core.Abstractions;

/// <summary>
/// Interface that all processors must implement
/// </summary>
public interface IProcessor
{
    Task ProcessAsync();
    IProcessor Init(ProcessorContext context);
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
    public string Summary { get; set; } = "";
    public string[] Enums { get; set; } = Array.Empty<string>();
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public ArgumentAction Action { get; set; } = ArgumentAction.Store;
    public string ParameterSetName { get; set; } = "(All)";
    public bool SupportsPipeline { get; set; } = false;
    public bool SupportsWildcards { get; set; } = false;
    public string SerializedName { get; set; } = "";
}

[AttributeUsage(AttributeTargets.Class)]
public class ProcessorInfoAttribute : Attribute
{
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Examples { get; set; } = Array.Empty<string>();
    public HelpTag[] Tags { get; set; } = Array.Empty<HelpTag>();
}

public enum HelpTag
{
    Preview,
    Deprecated,
    Experimental,
    GA,
}
