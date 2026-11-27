// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsKeyVault;
extern alias CmdletsMsGraph;

using System.Management.Automation;
using System.Reflection;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using AccountsCmdlets = Microsoft.Azure.Azclips.Cmdlets.Accounts;
using KeyVaultCmdlets = CmdletsKeyVault::Microsoft.Azure.Azclips.Cmdlets.KeyVault;
using ManagedServiceIdentityCmdlets = Microsoft.Azure.Azclips.Cmdlets.ManagedServiceIdentity;
using MsGraphCmdlets = CmdletsMsGraph::Microsoft.Azure.Azclips.Cmdlets.MsGraph;
using ResourcesCmdlets = Microsoft.Azure.Azclips.Cmdlets.Resources;

namespace Cmdlets.Tests.Commands;

/// <summary>
/// Pins the mandatory parameters of every parameter set of every cmdlet in <see cref="CmdletParameterTests"/>. That class
/// generates its missing-parameter cases from the [Parameter(Mandatory)] attributes, so a parameter that stops being
/// mandatory would drop its case without failing; here it fails, and the list must be updated on purpose.
/// </summary>
public sealed class MandatoryParameterTests
{
    private const string AllSets = ParameterAttribute.AllParameterSets;

    private static readonly Type[] s_cmdlets =
    [
        .. CmdletsIn(typeof(KeyVaultCmdlets.GetAzKeyVaultSecretCmdlet).Assembly),
        .. CmdletsIn(typeof(MsGraphCmdlets.GetAzMsGraphUserCmdlet).Assembly),
        .. CmdletsIn(typeof(ManagedServiceIdentityCmdlets.GetAzUserAssignedIdentityCmdlet).Assembly),
        .. CmdletsIn(typeof(ResourcesCmdlets.NewResourceGroupCmdlet).Assembly),
        .. CmdletsIn(typeof(AccountsCmdlets.ConnectAccountCmdlet).Assembly),
    ];

    // Cmdlet, parameter set (AllSets when the cmdlet names none), and its mandatory parameters in ordinal order.
    private static readonly (string Cmdlet, string Set, string[] Mandatory)[] s_pinned =
    [
        // KeyVault
        ("Add-AzKeyVaultCertificate", AllSets, ["Name"]),
        ("Export-AzKeyVaultCertificate", AllSets, ["FilePath", "Name"]),
        ("Get-AzKeyVaultCertificate", "Get", ["Name"]),
        ("Get-AzKeyVaultCertificate", "List", []),
        ("Get-AzKeyVaultCertificate", "Versions", ["IncludeVersions", "Name"]),
        ("Get-AzKeyVaultSecret", "Get", ["Name"]),
        ("Get-AzKeyVaultSecret", "List", []),
        ("Get-AzKeyVaultSecret", "Versions", ["IncludeVersions", "Name"]),
        ("Import-AzKeyVaultCertificate", AllSets, ["FilePath", "Name"]),
        ("Remove-AzKeyVaultCertificate", AllSets, ["Name"]),
        ("Remove-AzKeyVaultSecret", AllSets, ["Name"]),
        ("Set-AzKeyVaultSecret", AllSets, ["Name", "SecretValue"]),
        ("Undo-AzKeyVaultCertificateRemoval", AllSets, ["Name"]),
        ("Undo-AzKeyVaultSecretRemoval", AllSets, ["Name"]),
        ("Update-AzKeyVaultCertificate", AllSets, ["Name"]),
        ("Update-AzKeyVaultSecret", AllSets, ["Name"]),

        // MsGraph
        ("Get-AzMsGraphApplication", "DisplayName", ["DisplayName"]),
        ("Get-AzMsGraphApplication", "Get", ["Id"]),
        ("Get-AzMsGraphApplication", "List", []),
        ("Get-AzMsGraphGroup", "DisplayName", ["DisplayName"]),
        ("Get-AzMsGraphGroup", "DisplayNameStartsWith", ["DisplayNameStartsWith"]),
        ("Get-AzMsGraphGroup", "Get", ["ObjectId"]),
        ("Get-AzMsGraphGroup", "List", []),
        ("Get-AzMsGraphGroupMember", "DisplayName", ["GroupDisplayName"]),
        ("Get-AzMsGraphGroupMember", "ObjectId", ["GroupObjectId"]),
        ("Get-AzMsGraphUser", "DisplayName", ["DisplayName"]),
        ("Get-AzMsGraphUser", "Get", ["Id"]),
        ("Get-AzMsGraphUser", "List", []),
        ("Get-AzMsGraphUser", "Mail", ["Mail"]),
        ("Get-AzMsGraphUser", "SignedIn", ["SignedIn"]),
        ("Get-AzMsGraphUser", "StartsWith", ["StartsWith"]),
        ("New-AzMsGraphApplication", AllSets, ["DisplayName"]),
        ("Remove-AzMsGraphApplication", AllSets, ["Id"]),

        // ManagedServiceIdentity
        ("Get-AzFederatedIdentityCredential", "Get", ["IdentityName", "Name", "ResourceGroupName"]),
        ("Get-AzFederatedIdentityCredential", "List", ["IdentityName", "ResourceGroupName"]),
        ("Get-AzUserAssignedIdentity", "Get", ["Name", "ResourceGroupName"]),
        ("Get-AzUserAssignedIdentity", "List", []),
        ("New-AzFederatedIdentityCredential", AllSets, ["IdentityName", "Name", "ResourceGroupName"]),
        ("New-AzUserAssignedIdentity", AllSets, ["Location", "Name", "ResourceGroupName"]),
        ("Remove-AzFederatedIdentityCredential", AllSets, ["IdentityName", "Name", "ResourceGroupName"]),
        ("Remove-AzUserAssignedIdentity", AllSets, ["Name", "ResourceGroupName"]),
        ("Update-AzFederatedIdentityCredential", AllSets, ["IdentityName", "Name", "ResourceGroupName"]),
        ("Update-AzUserAssignedIdentity", AllSets, ["Name", "ResourceGroupName"]),

        // Resources
        ("Get-AzDeploymentOperation", "DeploymentObject", ["DeploymentObject"]),
        ("Get-AzDeploymentOperation", "Get", ["DeploymentName", "OperationIds"]),
        ("Get-AzDeploymentOperation", "List", ["DeploymentName"]),
        ("Get-AzManagementGroupDeployment", "GetByDeploymentId", ["Id"]),
        ("Get-AzManagementGroupDeployment", "GetByDeploymentName", ["ManagementGroupId"]),
        ("Get-AzManagementGroupDeploymentOperation", "DeploymentObject", ["DeploymentObject"]),
        ("Get-AzManagementGroupDeploymentOperation", "Get", ["DeploymentName", "ManagementGroupId", "OperationIds"]),
        ("Get-AzManagementGroupDeploymentOperation", "List", ["DeploymentName", "ManagementGroupId"]),
        ("Get-AzResourceGroup", "Get", ["Name"]),
        ("Get-AzResourceGroup", "List", []),
        ("Get-AzResourceGroupDeployment", "Get", ["Name", "ResourceGroupName"]),
        ("Get-AzResourceGroupDeployment", "GetById", ["Id"]),
        ("Get-AzResourceGroupDeployment", "List", ["ResourceGroupName"]),
        ("Get-AzResourceGroupDeploymentOperation", "Get", ["DeploymentName", "OperationIds", "ResourceGroupName"]),
        ("Get-AzResourceGroupDeploymentOperation", "List", ["DeploymentName", "ResourceGroupName"]),
        ("Get-AzRoleAssignment", "ResourceGroup", ["ResourceGroupName"]),
        ("Get-AzRoleAssignment", "Scope", ["Scope"]),
        ("Get-AzRoleAssignment", "SubscriptionScope", []),
        ("Get-AzSubDeployment", "Get", ["Name"]),
        ("Get-AzSubDeployment", "GetByInputObject", ["InputObject"]),
        ("Get-AzSubDeployment", "List", []),
        ("Get-AzSubDeploymentWhatIfResult", AllSets, ["Location"]),
        ("Get-AzTenantDeployment", "GetByDeploymentName", []),
        ("Get-AzTenantDeployment", "ListByFilter", ["Filter"]),
        ("Get-AzTenantDeploymentOperation", "DeploymentObject", ["DeploymentObject"]),
        ("Get-AzTenantDeploymentOperation", "Get", ["DeploymentName", "OperationIds"]),
        ("Get-AzTenantDeploymentOperation", "List", ["DeploymentName"]),
        ("Get-AzTenantDeploymentWhatIfResult", AllSets, ["Location"]),
        ("Invoke-AzRestMethod", AllSets, []),
        ("New-AzManagementGroupDeployment", AllSets, ["Location", "ManagementGroupId", "Name"]),
        ("New-AzResourceGroup", AllSets, ["Location", "Name"]),
        ("New-AzResourceGroupDeployment", AllSets, ["Name", "ResourceGroupName"]),
        ("New-AzResourceGroupDeploymentWhatIf", AllSets, ["Name", "ResourceGroupName"]),
        ("New-AzRoleAssignment", "ResourceGroup", ["ObjectId", "ResourceGroupName", "RoleDefinitionId"]),
        ("New-AzRoleAssignment", "Scope", ["ObjectId", "RoleDefinitionId", "Scope"]),
        ("New-AzRoleAssignment", "SubscriptionScope", ["ObjectId", "RoleDefinitionId"]),
        ("New-AzSubDeployment", AllSets, ["Location", "Mode", "Name"]),
        ("New-AzTenantDeployment", AllSets, ["Location"]),
        ("Remove-AzManagementGroupDeployment", "RemoveByDeploymentId", ["Id"]),
        ("Remove-AzManagementGroupDeployment", "RemoveByDeploymentName", ["ManagementGroupId", "Name"]),
        ("Remove-AzManagementGroupDeployment", "RemoveByInputObject", ["InputObject"]),
        ("Remove-AzResourceGroup", "RemoveByResourceGroupId", ["Id"]),
        ("Remove-AzResourceGroup", "RemoveByResourceGroupName", ["Name"]),
        ("Remove-AzResourceGroupDeployment", "RemoveByResourceGroupDeploymentId", ["Id"]),
        ("Remove-AzResourceGroupDeployment", "RemoveByResourceGroupName", ["Name", "ResourceGroupName"]),
        ("Remove-AzRoleAssignment", "ResourceGroup", ["Name", "ResourceGroupName"]),
        ("Remove-AzRoleAssignment", "Scope", ["Name", "Scope"]),
        ("Remove-AzRoleAssignment", "SubscriptionScope", ["Name"]),
        ("Remove-AzSubDeployment", "RemoveByDeploymentName", ["Name"]),
        ("Remove-AzSubDeployment", "RemoveByInputObject", ["InputObject"]),
        ("Remove-AzTenantDeployment", AllSets, ["Name"]),
        ("Save-AzResourceGroupDeploymentTemplate", AllSets, ["Name", "ResourceGroupName"]),
        ("Save-AzSubDeploymentTemplate", "SaveByDeploymentName", ["Name"]),
        ("Save-AzSubDeploymentTemplate", "SaveByInputObject", ["InputObject"]),
        ("Save-AzTenantDeploymentTemplate", AllSets, ["DeploymentName"]),
        ("Set-AzResourceGroup", "SetByResourceGroupId", ["Id", "Tag"]),
        ("Set-AzResourceGroup", "SetByResourceGroupName", ["Name", "Tag"]),
        ("Stop-AzManagementGroupDeployment", "StopByDeploymentId", ["Id"]),
        ("Stop-AzManagementGroupDeployment", "StopByDeploymentName", ["ManagementGroupId", "Name"]),
        ("Stop-AzManagementGroupDeployment", "StopByInputObject", ["InputObject"]),
        ("Stop-AzResourceGroupDeployment", AllSets, ["Name", "ResourceGroupName"]),
        ("Stop-AzSubDeployment", "StopByDeploymentName", ["Name"]),
        ("Stop-AzSubDeployment", "StopByInputObject", ["InputObject"]),
        ("Stop-AzTenantDeployment", AllSets, ["Name"]),
        ("Test-AzManagementGroupDeployment", AllSets, ["Location", "ManagementGroupId"]),
        ("Test-AzResourceGroupDeployment", AllSets, ["ResourceGroupName"]),
        ("Test-AzSubDeployment", AllSets, ["Location", "Mode", "Name"]),
        ("Test-AzTenantDeployment", AllSets, ["Location"]),

        // Accounts
        ("Connect-AzAccount", AllSets, []),
        ("Disconnect-AzAccount", AllSets, []),
        ("Get-AzAccessToken", "Resource", ["ResourceUrl"]),
        ("Get-AzAccessToken", "ResourceType", []),
        ("Get-AzAccessToken", "Scope", ["Scope"]),
        ("Get-AzProfile", "List", []),
        ("Get-AzProfile", "Show", ["Name"]),
        ("Get-AzProfile", "ShowActive", ["Active"]),
        ("Open-AzSurvey", AllSets, []),
        ("Remove-AzProfile", AllSets, ["Name"]),
        ("Set-AzProfile", AllSets, ["Name"]),
    ];

    public static TheoryData<string, string, string[]> GetPinnedParameterSets()
    {
        TheoryData<string, string, string[]> data = new();
        foreach ((string cmdlet, string set, string[] mandatory) in s_pinned)
        {
            data.Add(cmdlet, set, mandatory);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GetPinnedParameterSets))]
    public void MandatoryParameters_PinnedParameterSet_MatchCmdletAttributes(string cmdlet, string set, string[] pinnedMandatory)
    {
        Type cmdletType = s_cmdlets.Single(c => CommandName(c) == cmdlet);

        string[] mandatory = Parameters(cmdletType).Where(p => IsMandatory(p, set)).Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(pinnedMandatory, mandatory);
    }

    [Fact]
    public void ParameterSets_EveryCmdlet_ArePinned()
    {
        string[] pinnedSets = s_pinned.Select(p => $"{p.Cmdlet} {p.Set}").Order(StringComparer.Ordinal).ToArray();

        string[] parameterSets = s_cmdlets
            .SelectMany(c => ParameterSets(c).Select(s => $"{CommandName(c)} {s}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(pinnedSets, parameterSets);
    }

    private static Type[] CmdletsIn(Assembly cmdletAssembly) =>
        cmdletAssembly.GetTypes().Where(t => !t.IsAbstract && typeof(PSCmdlet).IsAssignableFrom(t) && t.IsDefined(typeof(CmdletAttribute))).ToArray();

    private static IEnumerable<PropertyInfo> Parameters(Type cmdlet) =>
        cmdlet.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.IsDefined(typeof(ParameterAttribute)));

    // The named parameter sets, including a default set that no parameter declares (such as a parameterless list
    // set), or AllSets when the cmdlet names none.
    private static string[] ParameterSets(Type cmdlet)
    {
        string? defaultSet = cmdlet.GetCustomAttribute<CmdletAttribute>()?.DefaultParameterSetName;
        string[] named = Parameters(cmdlet)
            .SelectMany(p => p.GetCustomAttributes<ParameterAttribute>())
            .Select(a => a.ParameterSetName)
            .Append(defaultSet)
            .Where(s => !string.IsNullOrEmpty(s) && s != AllSets)
            .Select(s => s!)
            .Distinct()
            .ToArray();
        return named.Length > 0 ? named : [AllSets];
    }

    private static bool IsMandatory(PropertyInfo parameter, string set) =>
        parameter.GetCustomAttributes<ParameterAttribute>().Any(a => a.Mandatory && (a.ParameterSetName == set || a.ParameterSetName == AllSets));
}