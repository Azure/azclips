using Extensions.Runtime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Models;

public class NetworkSecurityGroup
{
    public string? Id { get; set; }
    public string? Location { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public NetworkSecurityGroupProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class NetworkSecurityGroupProperties
{
    public bool FlushConnection { get; set; }
    public List<SecurityRule> SecurityRules { get; set; } = [];
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class SecurityRule
{
    public string Name { get; set; } = null!;
    public SecurityRuleProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class SecurityRuleProperties
{
    public string Protocol { get; set; } = null!;
    public string SourcePortRange { get; set; } = null!;
    public string DestinationPortRange { get; set; } = null!;
    public string SourceAddressPrefix { get; set; } = null!;
    public string DestinationAddressPrefix { get; set; } = null!;
    public string Access { get; set; } = null!;
    public int Priority { get; set; }
    public string Direction { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}