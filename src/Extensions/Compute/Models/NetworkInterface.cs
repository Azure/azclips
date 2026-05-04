using Extensions.Runtime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Models;

public class NetworkInterface
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Location { get; set; } = null!;
    public string ResourceGroupName { get; set; } = null!;
    public string ProvisioningState { get; set; } = null!;
    public List<IPConfiguration> IPConfigurations { get; set; } = null!;
    public NetworkSecurityGroup NetworkSecurityGroup { get; set; } = null!;

    public NetworkInterface()
    {

    }

    public NetworkInterface(string location, string name, string resourceGroupName, string id = "", string type = "", string provisioningState = "")
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

public class IPConfiguration
{
    public string Name { get; set; } = null!;
    public Subnet Subnet { get; set; } = null!;
    public string PrivateIpAllocationMethod = "Dynamic";
    public bool Primary = true;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}