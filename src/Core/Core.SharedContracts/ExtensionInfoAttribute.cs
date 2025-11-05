namespace Core.SharedContracts;

[AttributeUsage(AttributeTargets.Assembly)]
public class ExtensionInfoAttribute : Attribute
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
