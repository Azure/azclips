// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     The resource targeted by a deployment operation.
    public class TargetResource
    {
        // The Azure resource ID of the resource.
        public string? Id { get; set; }

        // The name of the resource.
        public string? ResourceName { get; set; }

        // The type of the resource.
        public string? ResourceType { get; set; }

        // The extension the resource was deployed with.
        public DeploymentExtensionDefinition? Extension { get; set; }

        // The extensible resource identifiers.
        public object? Identifiers { get; set; }

        // The API version the resource was deployed with.
        public string? ApiVersion { get; set; }

        // The symbolic name of the resource as defined in the deployment template.
        public string? SymbolicName { get; set; }

        // The symbolic name path to the resource in the deployment template, including nested
        // deployment(s) and extension if applicable.
        public List<string>? SymbolicNamePath { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     The deployment extension a resource was deployed with.
    public class DeploymentExtensionDefinition
    {
        // The alias of the extension as defined in the deployment template.
        public string? Alias { get; set; }

        // The extension name.
        public string? Name { get; set; }

        // The extension version.
        public string? Version { get; set; }

        // The extension configuration ID. Uniquely identifies a deployment target within an extension.
        public string? ConfigId { get; set; }

        // The extension configuration hash. Distinguishes configurations that share a config ID.
        public string? ConfigHash { get; set; }

        // The extension configuration.
        public Dictionary<string, DeploymentExtensionConfigItem>? Config { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     The value of a single extension config property.
    public class DeploymentExtensionConfigItem
    {
        // The value of the extension config property.
        public object? Value { get; set; }

        // The Azure Key Vault reference used to retrieve the secret value of the property.
        public KeyVaultParameterReference? KeyVaultReference { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Azure Key Vault parameter reference.
    public class KeyVaultParameterReference
    {
        // Azure Key Vault reference.
        public KeyVaultReference? KeyVault { get; set; }

        // Azure Key Vault secret name.
        public string? SecretName { get; set; }

        // Azure Key Vault secret version.
        public string? SecretVersion { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Azure Key Vault reference.
    public class KeyVaultReference
    {
        // Azure Key Vault resource id.
        public string? Id { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
