using System;

namespace Extension.Resource.Models;

public class ResourceGroup
{
    public ResourceGroup(string id, string name, string type, ResourceGroupProperties properties, string location, string managedBy, System.Collections.Generic.IDictionary<string, string> tags, DateTime? createdTime, DateTime? changedTime)
    {
        Id = id;
        Name = name;
        Type = type;
        Properties = properties;
        Location = location;
        ManagedBy = managedBy;
        Tags = tags;
        CreatedTime = createdTime;
        ChangedTime = changedTime;
    }

    public string Id { get; private set; }
    public string Name { get; private set; }
    public string Type { get; private set; }
    public ResourceGroupProperties Properties { get; set; }
    public string Location { get; set; }
    public string ManagedBy { get; set; }
    public System.Collections.Generic.IDictionary<string, string> Tags { get; set; }
    public System.DateTime? CreatedTime { get; private set; }
    public System.DateTime? ChangedTime { get; private set; }
}
