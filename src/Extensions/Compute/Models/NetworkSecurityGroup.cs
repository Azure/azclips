using Extensions.Runtime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Models;

public class NetworkSecurityGroup
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Location { get; set; } = null!;
    public string ResourceGroupName { get; set; } = null!;
    public string ProvisioningState { get; set; } = null!;
    public List<SecurityRule> SecurityRules { get; set; } = null!;

    public NetworkSecurityGroup()
    {

    }

    public NetworkSecurityGroup(string location, string name, string resourceGroupName, string id = "", string type = "", string provisioningState = "")
    {
        Id = id;
        Name = name;
        Type = type;
        Location = location;
        ResourceGroupName = resourceGroupName;
        ProvisioningState = provisioningState;
    }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class SecurityRule
{
    public string Name { get; set; } = null!;
    public string Direction { get; set; } = null!;
    public string Protocol { get; set; } = null!;
    public int Priority { get; set; }
    public string SourceAddressPrefix { get; set; } = null!;
    public string SourcePortRange { get; set; } = null!;
    public string DestinationPortRange { get; set; } = null!;
    public string DestinationAddressPrefix { get; set; } = null!;
    public string Access { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}