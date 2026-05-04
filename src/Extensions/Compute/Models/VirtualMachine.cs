using Extensions.Runtime.Json;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Models;

public class VirtualMachine
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Location { get; set; } = null!;
    public string ResourceGroupName { get; set; } = null!;
    public string ProvisioningState { get; set; } = null!;

    public VirtualMachine()
    {
        
    }
    
    public VirtualMachine(string location, string name, string resourceGroupName, string id = "", string type = "", string provisioningState = "")
    {
        Id = id;
        Name = name;
        Type = type;
        Location = location;
        ResourceGroupName = resourceGroupName;
        ProvisioningState = provisioningState;
    }

    public HardwareProfile HardwareProfile { get; set; } = null!;
    public OSProfile OSProfile { get; set; } = null!;
    public StorageProfile StorageProfile { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
public class HardwareProfile
{
    public string VmSize { get; set; }

    public HardwareProfile(string vmSize)
    {
        VmSize = vmSize;
    }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VMSizeProperties
{
    public int? VCPUsAvailable { get; set; }
    public int? VCPUsPerCore { get; set; }
    public VMSizeProperties(int? vCPUsAvailable, int? vCPUsPerCore)
    {
        VCPUsAvailable = vCPUsAvailable;
        VCPUsPerCore = vCPUsPerCore;
    }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class OSProfile
{
    public string AdminUsername { get; set; } = null!;
    public string AdminPassword { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class StorageProfile
{
    public OSDisk OSDisk { get; set; } = null!;
    public ImageReference ImageReference { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ImageReference
{
    public string Publisher {get; set; } = null!;
    public string Offer { get; set; } = null!;
    public string Sku { get; set; } = null!;
    public string Version { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class OSDisk
{
    public string CreateOption { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class NetworkProfile
{
    public IList<NetworkInterfaceReference> NetworkInterfaces { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class NetworkInterfaceReference
{
    public string Id { get; set; } = null!;
    public bool? Primary { get; set; } = null!;
    public string DeleteOption { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}