// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     Resource group information.
    public class ResourceGroup
    {
        public string Id { get; private set; }

        public string Name { get; private set; }

        public string Type { get; private set; }

        public string ProvisioningState { get; private set; }

        public string Location { get; set; }

        public string ManagedBy { get; set; }

        public IDictionary<string, string>? Tags { get; set; }

        public ResourceGroup(string location, string name, string id = "",  string type = "", string provisioningState = "", string managedBy = "", IDictionary<string, string>? tags = null)
        {
            Id = id;
            Name = name;
            Type = type;
            ProvisioningState = provisioningState;
            Location = location;
            ManagedBy = managedBy;
            Tags = tags;
        }
    }
}
