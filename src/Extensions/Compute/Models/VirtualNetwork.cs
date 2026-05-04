using Extensions.Runtime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Models;

public class VirtualNetwork
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Location { get; set; } = null!;
    public string ResourceGroupName { get; set; } = null!;
    public string ProvisioningState { get; set; } = null!;
    public List<Subnet> Subnets { get; set; } = null!;
    public AddressSpace AddressSpace { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class Subnet
{
    public string Name { get; set; } = null!;
    public List<string> AddressPrefix { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class AddressSpace
{
    public List<string> AddressPrefixes { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}