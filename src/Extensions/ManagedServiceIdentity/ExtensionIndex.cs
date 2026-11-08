// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;

namespace Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity
{
    [ExtensionVerb("identity")]
    public class ExtensionIndex : IExtensionIndex
    {
        private const string PSCommandPrefix = "Az";

        #region PS Command Metadata
        private static readonly CommandMetadata psIdentityCreate = new(
            Name: nameof(Processors.CreateIdentityProcessor),
            InvocationName: $"New-{PSCommandPrefix}UserAssignedIdentity",
            Aliases: [],
            FullName: typeof(Processors.CreateIdentityProcessor).FullName!,
            AssemblyName: typeof(Processors.CreateIdentityProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.CreateIdentityProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroup", "ResourceGroupName", typeof(string), "Resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("ResourceName", "Name", typeof(string), "Name of the user assigned identity.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"] = new("Location", "Location", typeof(string), "Location.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Tags"] = new("Tags", "Tags", typeof(Dictionary<string, string>), "Resource tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IsolationScope"] = new("IsolationScope", "IsolationScope", typeof(string), "Identity isolation scope.", IsRequired: false, Aliases: [], AllowedValues: ["None", "Regional"]),
                ["ResourceRestriction"] = new("ResourceRestrictionProviders", "ResourceRestriction", typeof(List<string>), "List of resource provider types that this identity can be assigned to.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata psIdentityList = new(
            Name: nameof(Processors.ListIdentityProcessor),
            InvocationName: $"Get-{PSCommandPrefix}UserAssignedIdentity_List",
            Aliases: [],
            FullName: typeof(Processors.ListIdentityProcessor).FullName!,
            AssemblyName: typeof(Processors.ListIdentityProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ListIdentityProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroup", "ResourceGroupName", typeof(string), "Resource group name. If not specified, lists all user-assigned identities in the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata psIdentityShow = new(
            Name: nameof(Processors.ShowIdentityProcessor),
            InvocationName: $"Get-{PSCommandPrefix}UserAssignedIdentity_Get",
            Aliases: [],
            FullName: typeof(Processors.ShowIdentityProcessor).FullName!,
            AssemblyName: typeof(Processors.ShowIdentityProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ShowIdentityProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroup", "ResourceGroupName", typeof(string), "Resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("ResourceName", "Name", typeof(string), "Name of the user assigned identity.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata psIdentityUpdate = new(
            Name: nameof(Processors.UpdateIdentityProcessor),
            InvocationName: $"Update-{PSCommandPrefix}UserAssignedIdentity",
            Aliases: [],
            FullName: typeof(Processors.UpdateIdentityProcessor).FullName!,
            AssemblyName: typeof(Processors.UpdateIdentityProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.UpdateIdentityProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroup", "ResourceGroupName", typeof(string), "Resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("ResourceName", "Name", typeof(string), "Name of the user assigned identity.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"] = new("Location", "Location", typeof(string), "Location.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Tags"] = new("Tags", "Tags", typeof(Dictionary<string, string>), "Resource tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["IsolationScope"] = new("IsolationScope", "IsolationScope", typeof(string), "Identity isolation scope.", IsRequired: false, Aliases: [], AllowedValues: ["None", "Regional"]),
                ["ResourceRestriction"] = new("ResourceRestrictionProviders", "ResourceRestriction", typeof(List<string>), "List of resource provider types that this identity can be assigned to.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata psIdentityDelete = new(
            Name: nameof(Processors.DeleteIdentityProcessor),
            InvocationName: $"Remove-{PSCommandPrefix}UserAssignedIdentity",
            Aliases: [],
            FullName: typeof(Processors.DeleteIdentityProcessor).FullName!,
            AssemblyName: typeof(Processors.DeleteIdentityProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.DeleteIdentityProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroup", "ResourceGroupName", typeof(string), "Resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("ResourceName", "Name", typeof(string), "Name of the user assigned identity.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata psFederatedCredentialCreate = new(
            Name: nameof(CreateFederatedCredentialProcessor_Orchestrator),
            InvocationName: $"New-{PSCommandPrefix}FederatedIdentityCredential",
            Aliases: [],
            FullName: typeof(CreateFederatedCredentialProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(CreateFederatedCredentialProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(CreateFederatedCredentialProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroup", "ResourceGroupName", typeof(string), "Resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["IdentityName"] = new("IdentityName", "IdentityName", typeof(string), "The name of the identity resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("Name", "Name", typeof(string), "The name of the federated identity credential resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Audience"] = new("Audiences", "Audience", typeof(List<string>), "The list of audiences that can appear in the issued token.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Issuer"] = new("Issuer", "Issuer", typeof(string), "The URL of the issuer to be trusted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Subject"] = new("Subject", "Subject", typeof(string), "The identifier of the external software workload. Either Subject or ClaimsMatchingExpressionValue must be defined, but not both.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ClaimsMatchingExpressionVersion"] = new("ClaimsMatchingExpressionVersion", "ClaimsMatchingExpressionVersion", typeof(int?), "Specifies the language version of the claims matching expression.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ClaimsMatchingExpressionValue"] = new("ClaimsMatchingExpressionValue", "ClaimsMatchingExpressionValue", typeof(string), "The wildcard-based expression for matching incoming claims. Cannot be used with Subject.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata psFederatedCredentialList = new(
            Name: nameof(Processors.ListFederatedCredentialProcessor),
            InvocationName: $"Get-{PSCommandPrefix}FederatedIdentityCredential_List",
            Aliases: [],
            FullName: typeof(Processors.ListFederatedCredentialProcessor).FullName!,
            AssemblyName: typeof(Processors.ListFederatedCredentialProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ListFederatedCredentialProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroup", "ResourceGroupName", typeof(string), "Resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["IdentityName"] = new("IdentityName", "IdentityName", typeof(string), "The name of the identity resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["SkipToken"] = new("SkipToken", "Skiptoken", typeof(string), "A skip token is used to continue retrieving items after an operation returns a partial result.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Top"] = new("Top", "Top", typeof(int?), "Number of records to return.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata psFederatedCredentialShow = new(
            Name: nameof(Processors.ShowFederatedCredentialProcessor),
            InvocationName: $"Get-{PSCommandPrefix}FederatedIdentityCredential_Get",
            Aliases: [],
            FullName: typeof(Processors.ShowFederatedCredentialProcessor).FullName!,
            AssemblyName: typeof(Processors.ShowFederatedCredentialProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ShowFederatedCredentialProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroup", "ResourceGroupName", typeof(string), "Resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["IdentityName"] = new("IdentityName", "IdentityName", typeof(string), "The name of the identity resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("Name", "Name", typeof(string), "The name of the federated identity credential resource.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata psFederatedCredentialUpdate = new(
            Name: nameof(UpdateFederatedCredentialProcessor_Orchestrator),
            InvocationName: $"Update-{PSCommandPrefix}FederatedIdentityCredential",
            Aliases: [],
            FullName: typeof(UpdateFederatedCredentialProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(UpdateFederatedCredentialProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(UpdateFederatedCredentialProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroup", "ResourceGroupName", typeof(string), "Resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["IdentityName"] = new("IdentityName", "IdentityName", typeof(string), "The name of the identity resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("Name", "Name", typeof(string), "The name of the federated identity credential resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Audience"] = new("Audiences", "Audience", typeof(List<string>), "The list of audiences that can appear in the issued token.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Issuer"] = new("Issuer", "Issuer", typeof(string), "The URL of the issuer to be trusted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Subject"] = new("Subject", "Subject", typeof(string), "The identifier of the external software workload. Either Subject or ClaimsMatchingExpressionValue must be defined, but not both.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ClaimsMatchingExpressionVersion"] = new("ClaimsMatchingExpressionVersion", "ClaimsMatchingExpressionVersion", typeof(int?), "Specifies the language version of the claims matching expression.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ClaimsMatchingExpressionValue"] = new("ClaimsMatchingExpressionValue", "ClaimsMatchingExpressionValue", typeof(string), "The wildcard-based expression for matching incoming claims. Cannot be used with Subject.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Set"] = new("Set", "Set", typeof(string[]), "Update an object by specifying a property path and value to set.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Add"] = new("Add", "Add", typeof(string[]), "Add an object to a list of objects by specifying a path and key value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Remove"] = new("Remove", "Remove", typeof(string[]), "Remove a property or an element from a list.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ForceString"] = new("ForceString", "ForceString", typeof(string), "When using 'Set' or 'Add', preserve string literals instead of attempting to convert to JSON.", IsRequired: false, Aliases: [], AllowedValues: ["0", "1", "f", "false", "n", "no", "t", "true", "y", "yes"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );

        private static readonly CommandMetadata psFederatedCredentialDelete = new(
            Name: nameof(Processors.DeleteFederatedCredentialProcessor),
            InvocationName: $"Remove-{PSCommandPrefix}FederatedIdentityCredential",
            Aliases: [],
            FullName: typeof(Processors.DeleteFederatedCredentialProcessor).FullName!,
            AssemblyName: typeof(Processors.DeleteFederatedCredentialProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.DeleteFederatedCredentialProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroup", "ResourceGroupName", typeof(string), "Resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["IdentityName"] = new("IdentityName", "IdentityName", typeof(string), "The name of the identity resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("Name", "Name", typeof(string), "The name of the federated identity credential resource.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        #endregion

        #region CLI Command Metadata
        private static readonly CommandMetadata cliIdentityCreate = new(
            Name: nameof(CreateIdentityProcessor_Orchestrator),
            InvocationName: "identity create",
            Aliases: [],
            FullName: typeof(CreateIdentityProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(CreateIdentityProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(CreateIdentityProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription"] = new("SubscriptionId", "subscription", typeof(string), "Name or ID of subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"] = new("ResourceGroup", "resource-group", typeof(string), "Name of resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["name"] = new("ResourceName", "name", typeof(string), "The name of the identity resource.", IsRequired: true, Aliases: ["resource-name", "n"], AllowedValues: []),
                ["location"] = new("Location", "location", typeof(string), "The geo-location where the resource lives.", IsRequired: true, Aliases: ["l"], AllowedValues: []),
                ["tags"] = new("TagsInput", "tags", typeof(string[]), "Resource tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["isolation-scope"] = new("IsolationScope", "isolation-scope", typeof(string), "Enum to configure regional restrictions on identity assignment, as necessary.", IsRequired: false, Aliases: [], AllowedValues: ["None", "Regional"]),
                ["resource-restriction"] = new("ResourceRestriction", "resource-restriction", typeof(string[]), "Restrictions on which resource providers this identity can be assigned to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["acquire-policy-token"] = new("AcquirePolicyToken", "acquire-policy-token", typeof(bool), "Acquiring an Azure Policy token automatically for this resource operation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["change-reference"] = new("ChangeReference", "change-reference", typeof(string), "The related change reference ID for this resource operation.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: CliAliases(("g", "resource-group"), ("n", "name"), ("resource-name", "name"), ("l", "location"))
        );

        private static readonly CommandMetadata cliIdentityList = new(
            Name: nameof(Processors.ListIdentityProcessor),
            InvocationName: "identity list",
            Aliases: [],
            FullName: typeof(Processors.ListIdentityProcessor).FullName!,
            AssemblyName: typeof(Processors.ListIdentityProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ListIdentityProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription"] = new("SubscriptionId", "subscription", typeof(string), "Name or ID of subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"] = new("ResourceGroup", "resource-group", typeof(string), "Name of resource group.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
            },
            ParameterAliases: CliAliases(("g", "resource-group"))
        );

        private static readonly CommandMetadata cliIdentityShow = new(
            Name: nameof(ShowIdentityProcessor_Orchestrator),
            InvocationName: "identity show",
            Aliases: [],
            FullName: typeof(ShowIdentityProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(ShowIdentityProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(ShowIdentityProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ids"] = new("Ids", "ids", typeof(string[]), "One or more resource IDs. Provide either --ids or other Resource Id arguments.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("ResourceName", "name", typeof(string), "The name of the identity resource.", IsRequired: false, Aliases: ["resource-name", "n"], AllowedValues: []),
                ["resource-group"] = new("ResourceGroup", "resource-group", typeof(string), "Name of resource group.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
                ["subscription"] = new("SubscriptionId", "subscription", typeof(string), "Name or ID of subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: CliAliases(("g", "resource-group"), ("n", "name"), ("resource-name", "name"))
        );

        private static readonly CommandMetadata cliIdentityUpdate = new(
            Name: nameof(UpdateIdentityProcessor_Orchestrator),
            InvocationName: "identity update",
            Aliases: [],
            FullName: typeof(UpdateIdentityProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(UpdateIdentityProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(UpdateIdentityProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["add"] = new("Add", "add", typeof(string[]), "Add an object to a list of objects by specifying a path and key value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["force-string"] = new("ForceString", "force-string", typeof(string), "When using 'set' or 'add', preserve string literals instead of attempting to convert to JSON.", IsRequired: false, Aliases: [], AllowedValues: ["0", "1", "f", "false", "n", "no", "t", "true", "y", "yes"]),
                ["ids"] = new("Ids", "ids", typeof(string[]), "One or more resource IDs. Provide either --ids or other Resource Id arguments.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["isolation-scope"] = new("IsolationScope", "isolation-scope", typeof(string), "Enum to configure regional restrictions on identity assignment, as necessary.", IsRequired: false, Aliases: [], AllowedValues: ["None", "Regional"]),
                ["name"] = new("ResourceName", "name", typeof(string), "The name of the identity resource.", IsRequired: false, Aliases: ["resource-name", "n"], AllowedValues: []),
                ["remove"] = new("Remove", "remove", typeof(string[]), "Remove a property or an element from a list.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"] = new("ResourceGroup", "resource-group", typeof(string), "Name of resource group.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
                ["resource-restriction"] = new("ResourceRestriction", "resource-restriction", typeof(string[]), "Restrictions on which resource providers this identity can be assigned to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["set"] = new("Set", "set", typeof(string[]), "Update an object by specifying a property path and value to set.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subscription"] = new("SubscriptionId", "subscription", typeof(string), "Name or ID of subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["tags"] = new("TagsInput", "tags", typeof(string[]), "Resource tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["acquire-policy-token"] = new("AcquirePolicyToken", "acquire-policy-token", typeof(bool), "Acquiring an Azure Policy token automatically for this resource operation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["change-reference"] = new("ChangeReference", "change-reference", typeof(string), "The related change reference ID for this resource operation.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: CliAliases(("g", "resource-group"), ("n", "name"), ("resource-name", "name"))
        );

        private static readonly CommandMetadata cliIdentityDelete = new(
            Name: nameof(DeleteIdentityProcessor_Orchestrator),
            InvocationName: "identity delete",
            Aliases: [],
            FullName: typeof(DeleteIdentityProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(DeleteIdentityProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(DeleteIdentityProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ids"] = new("Ids", "ids", typeof(string[]), "One or more resource IDs. Provide either --ids or other Resource Id arguments.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"] = new("ResourceName", "name", typeof(string), "The name of the identity resource.", IsRequired: false, Aliases: ["resource-name", "n"], AllowedValues: []),
                ["resource-group"] = new("ResourceGroup", "resource-group", typeof(string), "Name of resource group.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
                ["subscription"] = new("SubscriptionId", "subscription", typeof(string), "Name or ID of subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["acquire-policy-token"] = new("AcquirePolicyToken", "acquire-policy-token", typeof(bool), "Acquiring an Azure Policy token automatically for this resource operation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["change-reference"] = new("ChangeReference", "change-reference", typeof(string), "The related change reference ID for this resource operation.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: CliAliases(("g", "resource-group"), ("n", "name"), ("resource-name", "name"))
        );

        private static readonly CommandMetadata cliFederatedCredentialCreate = new(
            Name: nameof(CreateFederatedCredentialProcessor_Orchestrator),
            InvocationName: "identity federated-credential create",
            Aliases: [],
            FullName: typeof(CreateFederatedCredentialProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(CreateFederatedCredentialProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(CreateFederatedCredentialProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription"] = new("SubscriptionId", "subscription", typeof(string), "Name or ID of subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"] = new("ResourceGroup", "resource-group", typeof(string), "Name of resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["identity-name"] = new("IdentityName", "identity-name", typeof(string), "The name of the identity resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"] = new("Name", "name", typeof(string), "The name of the federated identity credential resource.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["audiences"] = new("Audiences", "audiences", typeof(List<string>), "The list of audiences that can appear in the issued token.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["issuer"] = new("Issuer", "issuer", typeof(string), "The URL of the issuer to be trusted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subject"] = new("Subject", "subject", typeof(string), "The identifier of the external software workload. Cannot be used with --claims-matching-expression-value.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["claims-matching-expression-version"] = new("ClaimsMatchingExpressionVersion", "claims-matching-expression-version", typeof(int?), "Specifies the version of the claims matching expression used in the expression.", IsRequired: false, Aliases: ["cme-version"], AllowedValues: []),
                ["claims-matching-expression-value"] = new("ClaimsMatchingExpressionValue", "claims-matching-expression-value", typeof(string), "The wildcard-based expression for matching incoming claims. Cannot be used with --subject.", IsRequired: false, Aliases: ["cme-value"], AllowedValues: []),
                ["acquire-policy-token"] = new("AcquirePolicyToken", "acquire-policy-token", typeof(bool), "Acquiring an Azure Policy token automatically for this resource operation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["change-reference"] = new("ChangeReference", "change-reference", typeof(string), "The related change reference ID for this resource operation.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: CliAliases(("g", "resource-group"), ("n", "name"), ("cme-version", "claims-matching-expression-version"), ("cme-value", "claims-matching-expression-value"))
        );

        private static readonly CommandMetadata cliFederatedCredentialList = new(
            Name: nameof(Processors.ListFederatedCredentialProcessor),
            InvocationName: "identity federated-credential list",
            Aliases: [],
            FullName: typeof(Processors.ListFederatedCredentialProcessor).FullName!,
            AssemblyName: typeof(Processors.ListFederatedCredentialProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ListFederatedCredentialProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription"] = new("SubscriptionId", "subscription", typeof(string), "Name or ID of subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"] = new("ResourceGroup", "resource-group", typeof(string), "Name of resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["identity-name"] = new("IdentityName", "identity-name", typeof(string), "The name of the identity resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["skiptoken"] = new("SkipToken", "skiptoken", typeof(string), "A skip token is used to continue retrieving items after an operation returns a partial result. If a previous response contains a nextLink element, the value of the nextLink element will include a skipToken parameter that specifies a starting point to use for subsequent calls.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["top"] = new("Top", "top", typeof(int?), "Number of records to return.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: CliAliases(("g", "resource-group"))
        );

        private static readonly CommandMetadata cliFederatedCredentialShow = new(
            Name: nameof(Processors.ShowFederatedCredentialProcessor),
            InvocationName: "identity federated-credential show",
            Aliases: [],
            FullName: typeof(Processors.ShowFederatedCredentialProcessor).FullName!,
            AssemblyName: typeof(Processors.ShowFederatedCredentialProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.ShowFederatedCredentialProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", "name", typeof(string), "The name of the federated identity credential resource.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["identity-name"] = new("IdentityName", "identity-name", typeof(string), "The name of the identity resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"] = new("ResourceGroup", "resource-group", typeof(string), "Name of resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["subscription"] = new("SubscriptionId", "subscription", typeof(string), "Name or ID of subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: CliAliases(("g", "resource-group"), ("n", "name"))
        );

        private static readonly CommandMetadata cliFederatedCredentialUpdate = new(
            Name: nameof(UpdateFederatedCredentialProcessor_Orchestrator),
            InvocationName: "identity federated-credential update",
            Aliases: [],
            FullName: typeof(UpdateFederatedCredentialProcessor_Orchestrator).FullName!,
            AssemblyName: typeof(UpdateFederatedCredentialProcessor_Orchestrator).Assembly.GetName().Name!,
            ProcessorType: typeof(UpdateFederatedCredentialProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["add"] = new("Add", "add", typeof(string[]), "Add an object to a list of objects by specifying a path and key value pairs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["force-string"] = new("ForceString", "force-string", typeof(string), "When using 'set' or 'add', preserve string literals instead of attempting to convert to JSON.", IsRequired: false, Aliases: [], AllowedValues: ["0", "1", "f", "false", "n", "no", "t", "true", "y", "yes"]),
                ["name"] = new("Name", "name", typeof(string), "The name of the federated identity credential resource.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["identity-name"] = new("IdentityName", "identity-name", typeof(string), "The name of the identity resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["remove"] = new("Remove", "remove", typeof(string[]), "Remove a property or an element from a list.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"] = new("ResourceGroup", "resource-group", typeof(string), "Name of resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["set"] = new("Set", "set", typeof(string[]), "Update an object by specifying a property path and value to set.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subscription"] = new("SubscriptionId", "subscription", typeof(string), "Name or ID of subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["audiences"] = new("Audiences", "audiences", typeof(List<string>), "The list of audiences that can appear in the issued token.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["issuer"] = new("Issuer", "issuer", typeof(string), "The URL of the issuer to be trusted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subject"] = new("Subject", "subject", typeof(string), "The identifier of the external software workload. Cannot be used with --claims-matching-expression-value.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["claims-matching-expression-version"] = new("ClaimsMatchingExpressionVersion", "claims-matching-expression-version", typeof(int?), "Specifies the version of the claims matching expression used in the expression.", IsRequired: false, Aliases: ["cme-version"], AllowedValues: []),
                ["claims-matching-expression-value"] = new("ClaimsMatchingExpressionValue", "claims-matching-expression-value", typeof(string), "The wildcard-based expression for matching incoming claims. Cannot be used with --subject.", IsRequired: false, Aliases: ["cme-value"], AllowedValues: []),
                ["acquire-policy-token"] = new("AcquirePolicyToken", "acquire-policy-token", typeof(bool), "Acquiring an Azure Policy token automatically for this resource operation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["change-reference"] = new("ChangeReference", "change-reference", typeof(string), "The related change reference ID for this resource operation.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: CliAliases(("g", "resource-group"), ("n", "name"), ("cme-version", "claims-matching-expression-version"), ("cme-value", "claims-matching-expression-value"))
        );

        private static readonly CommandMetadata cliFederatedCredentialDelete = new(
            Name: nameof(Processors.DeleteFederatedCredentialProcessor),
            InvocationName: "identity federated-credential delete",
            Aliases: [],
            FullName: typeof(Processors.DeleteFederatedCredentialProcessor).FullName!,
            AssemblyName: typeof(Processors.DeleteFederatedCredentialProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(Processors.DeleteFederatedCredentialProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["name"] = new("Name", "name", typeof(string), "The name of the federated identity credential resource.", IsRequired: true, Aliases: ["n"], AllowedValues: []),
                ["identity-name"] = new("IdentityName", "identity-name", typeof(string), "The name of the identity resource.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["resource-group"] = new("ResourceGroup", "resource-group", typeof(string), "Name of resource group.", IsRequired: true, Aliases: ["g"], AllowedValues: []),
                ["subscription"] = new("SubscriptionId", "subscription", typeof(string), "Name or ID of subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["acquire-policy-token"] = new("AcquirePolicyToken", "acquire-policy-token", typeof(bool), "Acquiring an Azure Policy token automatically for this resource operation.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["change-reference"] = new("ChangeReference", "change-reference", typeof(string), "The related change reference ID for this resource operation.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: CliAliases(("g", "resource-group"), ("n", "name"))
        );
        #endregion

        #region Command Tables
        private static readonly Dictionary<string, CommandMetadata> psTable = BuildCommandTable(
            psIdentityCreate,
            psIdentityList,
            psIdentityShow,
            psIdentityUpdate,
            psIdentityDelete,
            psFederatedCredentialCreate,
            psFederatedCredentialList,
            psFederatedCredentialShow,
            psFederatedCredentialUpdate,
            psFederatedCredentialDelete
        );

        private static readonly Dictionary<string, CommandMetadata> cliTable = BuildCommandTable(
            cliIdentityCreate,
            cliIdentityList,
            cliIdentityShow,
            cliIdentityUpdate,
            cliIdentityDelete,
            cliFederatedCredentialCreate,
            cliFederatedCredentialList,
            cliFederatedCredentialShow,
            cliFederatedCredentialUpdate,
            cliFederatedCredentialDelete
        );

        private static Dictionary<string, string> CliAliases(params (string Alias, string Canonical)[] aliases) =>
            aliases.ToDictionary(alias => alias.Alias, alias => alias.Canonical, StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, CommandMetadata> BuildCommandTable(params CommandMetadata[] commands) =>
            commands.ToDictionary(cmd => cmd.InvocationName, cmd => cmd);
        #endregion

        #region Interface Implementation
        public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
        {
            return GetCommandTable(clientType).TryGetValue(name, out var metadata)
                ? metadata
                : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
        }

        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => psTable,
                ClientType.CLI => cliTable,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }

        public IEnumerable<string> GetCommandKeys(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => psTable.Keys,
                ClientType.CLI => cliTable.Keys,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }

        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
        {
            return GetCommandTable(clientType).Values;
        }
        #endregion
    }
}
