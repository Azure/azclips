// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// The unified set of well-known resource-type shortcuts for token retrieval, shared by both the
/// CLI (<c>--resource-type</c>) and PowerShell (<c>-ResourceTypeName</c>) front ends. This is the
/// union of Azure CLI's shortcut list and Azure PowerShell's shortcut list, so the same shortcut
/// name resolves to the same resource regardless of front end.
/// </summary>
/// <remarks>
/// The resource URLs below are <b>public-cloud</b> values, verified against source:
/// <list type="bullet">
/// <item>CLI shortcuts + URLs: <c>azure-cli-core/azure/cli/core/cloud.py</c> (AzurePublicCloud) and
/// <c>command_modules/profile/__init__.py</c> (<c>cloud_resource_types</c>).</item>
/// <item>PowerShell shortcuts: <c>azure-powershell/src/Accounts/Accounts/Utilities/SupportedResourceNames.cs</c>;
/// URLs: <c>azure-powershell-common/src/Authentication.Abstractions/AzureEnvironmentConstants.cs</c>
/// (the <c>Azure*EndpointResourceId</c> constants).</item>
/// </list>
/// In both upstream tools these values are ultimately <b>cloud-environment-driven</b> (they vary by
/// sovereign cloud). This map hardcodes the public-cloud values and is structured so cloud-specific
/// values can be substituted when sovereign-cloud endpoint support is added.
///
/// Notes: <c>oss-rdbms</c> and <c>media</c> are CLI-only shortcuts; the remaining names come from
/// PowerShell. <c>storage</c> is a supported PowerShell name but is resolved via a separate
/// data-plane path upstream (not its <c>ResourceNameMap</c>); the standard public-cloud storage
/// resource is used here.
///
/// Lookup is case-insensitive and accepts both the CLI kebab-case spelling (e.g. <c>ms-graph</c>)
/// and the PowerShell PascalCase spelling (e.g. <c>MSGraph</c>).
/// </remarks>
public static class WellKnownResourceTypes
{
    /// <summary>The canonical shortcut name for Azure Resource Manager, used as the default target.</summary>
    public const string Arm = "arm";

    // Canonical shortcut name -> resource URL (public cloud). A token scope is derived as
    // "<resource>/.default" by TokenScopeResolver.
    private static readonly IReadOnlyDictionary<string, string> Canonical = new Dictionary<string, string>
    {
        [Arm]                   = "https://management.core.windows.net/",
        ["aad-graph"]           = "https://graph.windows.net/",
        ["ms-graph"]            = "https://graph.microsoft.com/",
        ["batch"]               = "https://batch.core.windows.net/",
        ["data-lake"]           = "https://datalake.azure.net/",
        ["oss-rdbms"]           = "https://ossrdbms-aad.database.windows.net",
        ["media"]               = "https://rest.media.azure.net",
        ["key-vault"]           = "https://vault.azure.net",
        ["storage"]             = "https://storage.azure.com/",
        ["synapse"]             = "https://dev.azuresynapse.net",
        ["managed-hsm"]         = "https://managedhsm.azure.net",
        ["communication-email"] = "https://communication.azure.com",
        ["attestation"]         = "https://attest.azure.net",
        ["app-configuration"]   = "https://azconfig.io",
        ["analysis-services"]   = "https://region.asazure.windows.net", // NB: the literal "region" is the fixed AAD resource ID Az uses (not a placeholder to substitute) - passed verbatim, matches azure-powershell.
        ["operational-insights"] = "https://api.loganalytics.io",
    };

    // Additional spellings/aliases (mainly the PowerShell PascalCase names) mapped to the canonical
    // key above. The canonical keys themselves are also accepted; matching is case-insensitive.
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
    {
        ["resourcemanager"]     = Arm,
        ["aadgraph"]            = "aad-graph",
        ["msgraph"]             = "ms-graph",
        ["datalake"]            = "data-lake",
        ["ossrdbms"]            = "oss-rdbms",
        ["keyvault"]            = "key-vault",
        ["managedhsm"]          = "managed-hsm",
        ["communicationemail"]  = "communication-email",
        ["appconfiguration"]    = "app-configuration",
        ["analysisservices"]    = "analysis-services",
        ["operationalinsights"] = "operational-insights",
    };

    private static readonly IReadOnlyDictionary<string, string> Lookup = BuildLookup();

    private static IReadOnlyDictionary<string, string> BuildLookup()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, resource) in Canonical)
        {
            map[name] = resource;
        }
        foreach (var (alias, canonical) in Aliases)
        {
            map[alias] = Canonical[canonical];
        }
        return map;
    }

    /// <summary>The canonical shortcut names (kebab-case), for help text and completion.</summary>
    public static IEnumerable<string> Names => Canonical.Keys;

    /// <summary>
    /// Attempts to resolve a well-known resource-type shortcut (in either CLI or PowerShell spelling)
    /// to its resource URL. Returns <see langword="true"/> on success.
    /// </summary>
    public static bool TryResolve(string? resourceTypeName, out string resourceUrl)
    {
        if (!string.IsNullOrWhiteSpace(resourceTypeName) &&
            Lookup.TryGetValue(resourceTypeName.Trim(), out var url))
        {
            resourceUrl = url;
            return true;
        }

        resourceUrl = string.Empty;
        return false;
    }
}
