namespace Core.SharedContracts;

[AttributeUsage(AttributeTargets.Class)]
public class CommandInfoAttribute : Attribute
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
