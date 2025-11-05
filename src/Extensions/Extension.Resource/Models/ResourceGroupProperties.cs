using System;

namespace Extension.Resource.Models;

public class ResourceGroupProperties
{
    public ResourceGroupProperties(string provisioningState)
    {
        ProvisioningState = provisioningState;
    }

    public string ProvisioningState { get; private set; }
}
