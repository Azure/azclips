// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     Resource group properties as ARM returns them, nested under "properties".
    public class ResourceGroupProperties
    {
        public string? ProvisioningState { get; set; }
    }

    //
    // Summary:
    //     Resource group information.
    public class ResourceGroup
    {
        public string Id { get; private set; }

        public string Name { get; private set; }

        public string Type { get; private set; }

        // Null when the model is used as a request body, so creates don't send "properties": null.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ResourceGroupProperties? Properties { get; set; }

        public string Location { get; set; }

        public string ManagedBy { get; set; }

        public IDictionary<string, string>? Tags { get; set; }

        public ResourceGroup(string location, string name, string id = "",  string type = "", string managedBy = "", IDictionary<string, string>? tags = null)
        {
            Id = id;
            Name = name;
            Type = type;
            Location = location;
            ManagedBy = managedBy;
            Tags = tags;
        }
    }
}
