// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Accounts;

/// <summary>
/// The unified set of well-known resource-type shortcuts for token retrieval, shared by both the
/// CLI (<c>--resource-type</c>) and PowerShell (<c>-ResourceTypeName</c>) front ends. This is the
/// union of Azure CLI's shortcut list and Azure PowerShell's shortcut list, so the same shortcut
/// resolves to the same resource whichever front end asked.
/// </summary>
/// <remarks>
/// This resolver is surface-agnostic and understands <b>both</b> spellings (see <see cref="Lookup"/>),
/// but each front end enforces only its own vocabulary at the edge: the CLI accepts the kebab-case
/// names (via <c>AllowedValues</c> in the command metadata), and PowerShell accepts the PascalCase
/// names (via <c>[ValidateSet]</c> on <c>Get-AzAccessToken</c>). This mirrors the classic tools, where
/// <c>az</c> takes kebab-case and <c>Get-AzAccessToken</c> takes PascalCase. Because the resolver knows
/// both, each surface simply feeds it its own spelling.
///
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
/// <see cref="Lookup"/> is case-insensitive and accepts both the CLI kebab-case spelling (e.g.
/// <c>ms-graph</c>) and the PowerShell PascalCase spelling (e.g. <c>MSGraph</c>).
/// </remarks>
internal static class WellKnownResourceTypes
{
    /// <summary>The canonical shortcut name for Azure Resource Manager, used as the default target.</summary>
    public const string Arm = "arm";

    // Single source of truth: one row per well-known resource type carrying its CLI kebab-case spelling,
    // its PowerShell PascalCase spelling (null for CLI-only shortcuts), and the public-cloud resource URL.
    // A token scope is derived as "<resource>/.default" by TokenScopeResolver. Adding a type is a single
    // row here. The CLI surface uses the kebab names and the PowerShell surface uses the PascalCase names,
    // so a row with a null Pascal spelling is offered on the CLI only.
    private static readonly (string Kebab, string? Pascal, string Resource)[] All =
    [
        (Arm,                    "Arm",                "https://management.core.windows.net/"),
        ("aad-graph",            "AadGraph",           "https://graph.windows.net/"),
        ("ms-graph",             "MSGraph",            "https://graph.microsoft.com/"),
        ("batch",                "Batch",              "https://batch.core.windows.net/"),
        ("data-lake",            "DataLake",           "https://datalake.azure.net/"),
        ("oss-rdbms",            null,                 "https://ossrdbms-aad.database.windows.net"), // CLI-only shortcut; no upstream PowerShell spelling, so not offered on the PS surface.
        ("media",                null,                 "https://rest.media.azure.net"),             // CLI-only shortcut; no upstream PowerShell spelling, so not offered on the PS surface.
        ("key-vault",            "KeyVault",           "https://vault.azure.net"),
        ("storage",              "Storage",            "https://storage.azure.com/"),               // Data-plane resource id upstream; standard public-cloud storage resource used here.
        ("synapse",              "Synapse",            "https://dev.azuresynapse.net"),
        ("managed-hsm",          "ManagedHsm",         "https://managedhsm.azure.net"),
        ("communication-email",  "CommunicationEmail", "https://communication.azure.com"),
        ("attestation",          "Attestation",        "https://attest.azure.net"),
        ("app-configuration",    "AppConfiguration",   "https://azconfig.io"),
        ("analysis-services",    "AnalysisServices",   "https://region.asazure.windows.net"),       // NB: the literal "region" is the fixed AAD resource id Az uses (not a placeholder to substitute) - passed verbatim, matches azure-powershell.
        ("operational-insights", "OperationalInsights", "https://api.loganalytics.io"),
    ];

    // Extra accepted spellings that are neither the kebab nor the PascalCase name above. Kept minimal:
    // only genuine synonyms live here. "ResourceManager" is an additional PowerShell spelling for arm,
    // advertised on the PowerShell surface (matching Azure PowerShell) alongside "Arm".
    private static readonly (string Alias, string Kebab)[] ExtraAliases =
    [
        ("ResourceManager", Arm),
    ];

    private static readonly IReadOnlyDictionary<string, string> Lookup = BuildLookup();

    private static IReadOnlyDictionary<string, string> BuildLookup()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (kebab, pascal, resource) in All)
        {
            map[kebab] = resource;
            if (pascal is not null)
            {
                map[pascal] = resource;
            }
        }
        foreach (var (alias, kebab) in ExtraAliases)
        {
            map[alias] = map[kebab];
        }
        return map;
    }

    /// <summary>The canonical shortcut names (kebab-case), for CLI help text and tab completion.</summary>
    public static string[] Names { get; } = [.. All.Select(e => e.Kebab)];

    /// <summary>
    /// The PowerShell PascalCase spellings (only the rows that have one). CLI-only shortcuts such as
    /// oss-rdbms and media have no PowerShell spelling and are excluded, matching Azure PowerShell's
    /// resource-name list. Used to keep the Get-AzAccessToken -ResourceTypeName ValidateSet in lockstep.
    /// </summary>
    public static string[] PowerShellNames { get; } = [.. All.Where(e => e.Pascal is not null).Select(e => e.Pascal!)];

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
