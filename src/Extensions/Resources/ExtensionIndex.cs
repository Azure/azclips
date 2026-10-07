// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Resources
{
    [ExtensionVerb("group")]
    [ExtensionVerb("deployment")]
    [ExtensionVerb("role")]
    [ExtensionVerb("rest")]
    public class ExtensionIndex : IExtensionIndex
    {
        // ── PS Command Metadata ──
        private static readonly CommandMetadata s_newResourceGroup_PS = new(
            Name: "NewResourceGroupProcessor",
            InvocationName: "New-AzResourceGroup",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]           = new("Name", "Name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Location"]       = new("Location", "Location", typeof(string), "Location for resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_setResourceGroup_PS = new(
            Name: "SetResourceGroupProcessor_Orchestrator",
            InvocationName: "Set-AzResourceGroup",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.SetResourceGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.SetResourceGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]           = new("ResourceGroupName", "Name", typeof(string), "The name of the resource group.", IsRequired: false, Aliases: ["ResourceGroupName"], AllowedValues: []),
                ["Id"]             = new("Id", "Id", typeof(string), "The resource group ID.", IsRequired: false, Aliases: ["ResourceGroupId", "ResourceId"], AllowedValues: []),
                ["Tag"]            = new("Tags", "Tag", typeof(Dictionary<string, string>), "The complete set of resource group tags.", IsRequired: true, Aliases: ["Tags"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceGroupName"] = "Name",
                ["ResourceGroupId"] = "Id",
                ["ResourceId"] = "Id",
                ["Tags"] = "Tag",
            }
        );
        private static readonly CommandMetadata s_removeResourceGroup_PS = new(
            Name: "DeleteResourceGroupProcessor_Orchestrator",
            InvocationName: "Remove-AzResourceGroup",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeleteResourceGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.DeleteResourceGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("ResourceGroupName", "Name", typeof(string), "The name of the resource group.", IsRequired: false, Aliases: ["ResourceGroupName"], AllowedValues: []),
                ["Id"]                = new("Id", "Id", typeof(string), "The resource group ID.", IsRequired: false, Aliases: ["ResourceGroupId", "ResourceId"], AllowedValues: []),
                ["ForceDeletionType"] = new("ForceDeletionTypes", "ForceDeletionType", typeof(string), "The resource types to force delete.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["ResourceGroupName"] = "Name",
                ["ResourceGroupId"] = "Id",
                ["ResourceId"] = "Id",
            }
        );
        private static readonly CommandMetadata s_getResourceGroup_PS = new(
            Name: "GetResourceGroupProcessor",
            InvocationName: "Get-AzResourceGroup_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.GetResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]           = new("Name", "Name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listResourceGroup_PS = new(
            Name: "ListResourceGroupProcessor",
            InvocationName: "Get-AzResourceGroup_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listRoleAssignment_PS = new(
            Name: "ListRoleAssignmentProcessor_Orchestrator",
            InvocationName: "Get-AzRoleAssignment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListRoleAssignmentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListRoleAssignmentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Scope"]             = new("Scope", "Scope", typeof(string), "Fully qualified scope to list role assignments at. Mutually exclusive with -ResourceGroupName. Defaults to the subscription scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "List role assignments scoped to this resource group under the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ObjectId"]          = new("AssigneeObjectId", "ObjectId", typeof(string), "Filter to role assignments for this principal's object (principal) ID (GUID).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RoleDefinitionId"]  = new("RoleDefinitionId", "RoleDefinitionId", typeof(string), "Filter to role assignments for this role definition ID (GUID). Role name resolution is not yet supported.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newRoleAssignment_PS = new(
            Name: "NewRoleAssignmentProcessor_Orchestrator",
            InvocationName: "New-AzRoleAssignment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewRoleAssignmentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewRoleAssignmentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Scope"]             = new("Scope", "Scope", typeof(string), "Fully qualified scope to create the role assignment at. Mutually exclusive with -ResourceGroupName. Defaults to the subscription scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "Create the role assignment scoped to this resource group under the subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ObjectId"]          = new("AssigneeObjectId", "ObjectId", typeof(string), "The principal's object (principal) ID (GUID) to assign the role to.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["RoleDefinitionId"]  = new("RoleDefinitionId", "RoleDefinitionId", typeof(string), "The role to assign: a role definition ID (GUID) or a fully-qualified role definition ID. Role name resolution is not yet supported.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ObjectType"]        = new("PrincipalType", "ObjectType", typeof(string), "The principal type of the assigned principal (User, Group, ServicePrincipal, or ForeignGroup).", IsRequired: false, Aliases: [], AllowedValues: ["User", "Group", "ServicePrincipal", "ForeignGroup"]),
                ["Name"]              = new("Name", "Name", typeof(string), "The role assignment name (a GUID). Generated when omitted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Description"]       = new("Description", "Description", typeof(string), "Description of the role assignment.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Condition"]         = new("Condition", "Condition", typeof(string), "Condition under which the principal is granted permission.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ConditionVersion"]  = new("ConditionVersion", "ConditionVersion", typeof(string), "Version of the condition syntax (currently only '2.0').", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newResourceGroupDeployment_PS = new(
            Name: "NewResourceGroupDeploymentProcessor_Orchestrator",
            InvocationName: "New-AzResourceGroupDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The name of the resource group to deploy into.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["TemplateUri"]       = new("TemplateUri", "TemplateUri", typeof(string), "The URI of the ARM template to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateFile"]      = new("TemplateFile", "TemplateFile", typeof(string), "The path to a local ARM template file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateSpecId"]    = new("TemplateSpecId", "TemplateSpecId", typeof(string), "The resource id of a Template Spec to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["QueryString"]       = new("QueryString", "QueryString", typeof(string), "The query string (such as a SAS token) to use with the template URI and/or the parameters URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersUri"]     = new("ParametersUri", "ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersFile"]    = new("ParametersFile", "ParametersFile", typeof(string), "The path to a local parameters file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Mode"]              = new("Mode", "Mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["Parameter"]         = new("Parameter", "Parameter", typeof(string[]), "Inline deployment parameter(s) in KEY=VALUE form (repeatable). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RollbackOnError"]   = new("RollbackOnError", "RollbackOnError", typeof(bool), "Roll the resource group back to the last successful deployment on error.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["RollbackToDeploymentName"] = new("RollbackToDeploymentName", "RollbackToDeploymentName", typeof(string), "Roll the resource group back to a specific named deployment on error. Mutually exclusive with RollbackOnError.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ValidationLevel"]   = new("ValidationLevel", "ValidationLevel", typeof(string), "The validation level of the deployment.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_rest_PS = new(
            Name: "RestProcessor_Orchestrator",
            InvocationName: "Invoke-AzRestMethod",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.RestProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.RestProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["Method"]  = new("Method", "Method", typeof(string), "HTTP method (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS). Defaults to GET.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Uri"]     = new("Uri", "Uri", typeof(string), "Absolute URL or relative ARM path. Relative paths are prefixed with https://management.azure.com. The token {subscriptionId} is replaced with the current subscription. Mutually exclusive with the piece-wise ByParameters inputs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Payload"] = new("Payload", "Payload", typeof(string), "Optional request body (typically JSON); sent whenever provided, for any method.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Headers"] = new("Headers", "Headers", typeof(List<string>), "Optional request headers in KEY=VALUE form.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["SubscriptionId"]       = new("SubscriptionId", "SubscriptionId", typeof(string), "Subscription id used to build an ARM URL from piece-wise parameters. Defaults to the current subscription. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"]    = new("ResourceGroupName", "ResourceGroupName", typeof(string), "Resource group name segment for a piece-wise ARM URL. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceProviderName"] = new("ResourceProviderName", "ResourceProviderName", typeof(string), "Resource provider namespace (e.g. Microsoft.Resources) for a piece-wise ARM URL. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceType"]         = new("ResourceType", "ResourceType", typeof(List<string>), "Resource type segments, interleaved with Name, for a piece-wise ARM URL. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]                 = new("Name", "Name", typeof(List<string>), "Resource name segments, interleaved with ResourceType (may have one fewer entry for a collection GET). Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ApiVersion"]           = new("ApiVersion", "ApiVersion", typeof(string), "api-version for a piece-wise ARM URL. Required when building from parameters. Mutually exclusive with Uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["UriParameters"]        = new("UriParameters", "UriParameters", typeof(List<string>), "Optional query-string parameters in KEY=VALUE form, appended to the final URL. Allowed in both URI and ByParameters modes.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newResourceGroupDeploymentWhatIf_PS = new(
            Name: "NewResourceGroupDeploymentWhatIfProcessor_Orchestrator",
            InvocationName: "New-AzResourceGroupDeploymentWhatIf",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupDeploymentWhatIfProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupDeploymentWhatIfProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The name of the resource group to run the what-if against.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["TemplateUri"]       = new("TemplateUri", "TemplateUri", typeof(string), "The URI of the ARM template to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateFile"]      = new("TemplateFile", "TemplateFile", typeof(string), "The path to a local ARM template file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateSpecId"]    = new("TemplateSpecId", "TemplateSpecId", typeof(string), "The resource id of a Template Spec to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["QueryString"]       = new("QueryString", "QueryString", typeof(string), "The query string (such as a SAS token) to use with the template URI and/or the parameters URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersUri"]     = new("ParametersUri", "ParametersUri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ParametersFile"]    = new("ParametersFile", "ParametersFile", typeof(string), "The path to a local parameters file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Mode"]              = new("Mode", "Mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["Parameter"]         = new("Parameter", "Parameter", typeof(string[]), "Inline deployment parameter(s) in KEY=VALUE form (repeatable). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with ParametersUri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ValidationLevel"]   = new("ValidationLevel", "ValidationLevel", typeof(string), "The validation level of the what-if operation.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["ResultFormat"]      = new("ResultFormat", "ResultFormat", typeof(string), "The format of the what-if results. FullResourcePayloads (default) or ResourceIdOnly.", IsRequired: false, Aliases: [], AllowedValues: ["FullResourcePayloads", "ResourceIdOnly"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newManagementGroupDeployment_PS = new(
            Name: "NewManagementGroupDeploymentProcessor_Orchestrator",
            InvocationName: "New-AzManagementGroupDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewManagementGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewManagementGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ManagementGroupId"] = new("ManagementGroupId", "ManagementGroupId", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("Name", "Name", typeof(string), "The deployment name. Derived when omitted.", IsRequired: false, Aliases: ["DeploymentName"], AllowedValues: []),
                ["Location"] = new("Location", "Location", typeof(string), "The location used to store deployment metadata.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["TemplateFile"] = new("TemplateFile", "TemplateFile", typeof(string), "A local JSON or Bicep template file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateUri"] = new("TemplateUri", "TemplateUri", typeof(string), "The template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateSpecId"] = new("TemplateSpecId", "TemplateSpecId", typeof(string), "The template spec resource ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateObject"] = new("TemplateObject", "TemplateObject", typeof(object), "An inline template object.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["QueryString"] = new("QueryString", "QueryString", typeof(string), "A query string for linked template or parameter URIs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateParameterFile"] = new("ParametersFile", "TemplateParameterFile", typeof(string), "A local JSON or bicepparam file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateParameterUri"] = new("ParametersUri", "TemplateParameterUri", typeof(string), "The parameter file URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateParameterObject"] = new("ParametersObject", "TemplateParameterObject", typeof(object), "An inline parameter object.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Parameter"] = new("Parameter", "Parameter", typeof(string[]), "Inline KEY=VALUE parameter overlays.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ValidationLevel"] = new("ValidationLevel", "ValidationLevel", typeof(string), "The validation level.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["Tag"] = new("Tags", "Tag", typeof(object), "Deployment tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["DeploymentDebugLogLevel"] = new("DeploymentDebugLogLevel", "DeploymentDebugLogLevel", typeof(string), "Deployment debug detail level.", IsRequired: false, Aliases: [], AllowedValues: ["RequestContent", "ResponseContent", "All", "None"]),
                ["WhatIf"] = new("WhatIf", "WhatIf", typeof(bool), "Run an ARM what-if operation instead of creating the deployment.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["WhatIfResultFormat"] = new("WhatIfResultFormat", "WhatIfResultFormat", typeof(string), "The what-if result format.", IsRequired: false, Aliases: [], AllowedValues: ["FullResourcePayloads", "ResourceIdOnly"]),
                ["WhatIfExcludeChangeType"] = new("WhatIfExcludeChangeType", "WhatIfExcludeChangeType", typeof(string[]), "What-if change types to exclude.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Azure profile name.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["DeploymentName"] = "Name" }
        );
        private static readonly CommandMetadata s_validateManagementGroupDeployment_PS = new(
            Name: "TestManagementGroupDeploymentProcessor_Orchestrator",
            InvocationName: "Test-AzManagementGroupDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.TestManagementGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.TestManagementGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ManagementGroupId"] = new("ManagementGroupId", "ManagementGroupId", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("Name", "Name", typeof(string), "The deployment name. Derived when omitted.", IsRequired: false, Aliases: ["DeploymentName"], AllowedValues: []),
                ["Location"] = new("Location", "Location", typeof(string), "The location used to store deployment metadata.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["TemplateFile"] = new("TemplateFile", "TemplateFile", typeof(string), "A local JSON or Bicep template file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateUri"] = new("TemplateUri", "TemplateUri", typeof(string), "The template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateSpecId"] = new("TemplateSpecId", "TemplateSpecId", typeof(string), "The template spec resource ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateObject"] = new("TemplateObject", "TemplateObject", typeof(object), "An inline template object.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["QueryString"] = new("QueryString", "QueryString", typeof(string), "A query string for linked template or parameter URIs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateParameterFile"] = new("ParametersFile", "TemplateParameterFile", typeof(string), "A local JSON or bicepparam file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateParameterUri"] = new("ParametersUri", "TemplateParameterUri", typeof(string), "The parameter file URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["TemplateParameterObject"] = new("ParametersObject", "TemplateParameterObject", typeof(object), "An inline parameter object.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Parameter"] = new("Parameter", "Parameter", typeof(string[]), "Inline KEY=VALUE parameter overlays.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["ValidationLevel"] = new("ValidationLevel", "ValidationLevel", typeof(string), "The validation level.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Azure profile name.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["DeploymentName"] = "Name" }
        );
        private static readonly CommandMetadata s_getManagementGroupDeployment_PS = new(
            Name: "GetAtManagementGroupScopeDeploymentsProcessor",
            InvocationName: "Get-AzManagementGroupDeployment_Get",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetAtManagementGroupScopeDeploymentsProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.GetAtManagementGroupScopeDeploymentsProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ManagementGroupId"] = new("GroupId", "ManagementGroupId", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("DeploymentName", "Name", typeof(string), "The deployment name.", IsRequired: true, Aliases: ["DeploymentName"], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Azure profile name.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["DeploymentName"] = "Name" }
        );
        private static readonly CommandMetadata s_listManagementGroupDeployment_PS = new(
            Name: "ListAtManagementGroupScopeDeploymentsProcessor",
            InvocationName: "Get-AzManagementGroupDeployment_List",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListAtManagementGroupScopeDeploymentsProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListAtManagementGroupScopeDeploymentsProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ManagementGroupId"] = new("GroupId", "ManagementGroupId", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Azure profile name.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_stopManagementGroupDeployment_PS = new(
            Name: "StopManagementGroupDeploymentProcessor_Orchestrator",
            InvocationName: "Stop-AzManagementGroupDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.StopManagementGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.StopManagementGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ManagementGroupId"] = new("ManagementGroupId", "ManagementGroupId", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("Name", "Name", typeof(string), "The deployment name.", IsRequired: true, Aliases: ["DeploymentName"], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Azure profile name.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["DeploymentName"] = "Name" }
        );
        private static readonly CommandMetadata s_removeManagementGroupDeployment_PS = new(
            Name: "DeleteAtManagementGroupScopeDeploymentsProcessor",
            InvocationName: "Remove-AzManagementGroupDeployment",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeleteAtManagementGroupScopeDeploymentsProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.DeleteAtManagementGroupScopeDeploymentsProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["ManagementGroupId"] = new("GroupId", "ManagementGroupId", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["Name"] = new("DeploymentName", "Name", typeof(string), "The deployment name.", IsRequired: true, Aliases: ["DeploymentName"], AllowedValues: []),
                ["Profile"] = new("Profile", "Profile", typeof(string), "Azure profile name.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string> { ["DeploymentName"] = "Name" }
        );
        // ── CLI Command Metadata ──
        private static readonly CommandMetadata s_newResourceGroup_CLI = new(
            Name: "NewResourceGroupProcessor",
            InvocationName: "group create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["location"]        = new("Location", "location", typeof(string), "Location for resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_updateResourceGroup_CLI = new(
            Name: "UpdateResourceGroupProcessor_Orchestrator",
            InvocationName: "group update",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.UpdateResourceGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.UpdateResourceGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("ResourceGroupName", "name", typeof(string), "Name of resource group.", IsRequired: true, Aliases: ["n", "resource-group", "g"], AllowedValues: []),
                ["tags"]            = new("TagsInput", "tags", typeof(string[]), "Space-separated tags in key[=value] format. Use an empty string to clear existing tags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["set"]             = new("Set", "set", typeof(string[]), "Update a property by specifying a property path and value.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["force-string"]    = new("ForceString", "force-string", typeof(bool), "Preserve string literals when using --set instead of attempting to convert them to JSON.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["resource-group"] = "name",
                ["g"] = "name",
            }
        );
        private static readonly CommandMetadata s_getResourceGroup_CLI = new(
            Name: "GetResourceGroupProcessor",
            InvocationName: "group show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.GetResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_existsResourceGroup_CLI = new(
            Name: "ExistsResourceGroupProcessor_Orchestrator",
            InvocationName: "group exists",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ExistsResourceGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ExistsResourceGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("ResourceGroupName", "name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: ["n", "resource-group", "g"], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["resource-group"] = "name",
                ["g"] = "name",
            }
        );
        private static readonly CommandMetadata s_deleteResourceGroup_CLI = new(
            Name: "DeleteResourceGroupProcessor_Orchestrator",
            InvocationName: "group delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeleteResourceGroupProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.DeleteResourceGroupProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]      = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]                 = new("ResourceGroupName", "name", typeof(string), "The name of the resource group.", IsRequired: true, Aliases: ["n", "resource-group", "g"], AllowedValues: []),
                ["force-deletion-types"] = new("ForceDeletionTypes", "force-deletion-types", typeof(string), "The resource type to force delete.", IsRequired: false, Aliases: ["f"], AllowedValues: ["Microsoft.Compute/virtualMachines", "Microsoft.Compute/virtualMachineScaleSets", "Microsoft.Databricks/workspaces"]),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["resource-group"] = "name",
                ["g"] = "name",
                ["f"] = "force-deletion-types",
            }
        );
        private static readonly CommandMetadata s_listResourceGroup_CLI = new(
            Name: "ListResourceGroupProcessor",
            InvocationName: "group list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListResourceGroupProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListResourceGroupProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listRoleAssignment_CLI = new(
            Name: "ListRoleAssignmentProcessor_Orchestrator",
            InvocationName: "role assignment list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListRoleAssignmentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListRoleAssignmentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]    = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["scope"]              = new("Scope", "scope", typeof(string), "Fully qualified scope to list role assignments at. Mutually exclusive with --resource-group. Defaults to the subscription scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]     = new("ResourceGroupName", "resource-group", typeof(string), "List role assignments scoped to this resource group under the subscription.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
                ["assignee"]           = new("Assignee", "assignee", typeof(string), "Filter to role assignments for this principal. Accepts an object (principal) ID; name/UPN/service-principal resolution is not yet supported.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["assignee-object-id"] = new("AssigneeObjectId", "assignee-object-id", typeof(string), "Filter to role assignments for this principal's object (principal) ID (GUID).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["role"]               = new("RoleDefinitionId", "role", typeof(string), "Filter to role assignments for this role definition ID (GUID). Role name resolution is not yet supported.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
            }
        );
        private static readonly CommandMetadata s_newRoleAssignment_CLI = new(
            Name: "NewRoleAssignmentProcessor_Orchestrator",
            InvocationName: "role assignment create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewRoleAssignmentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewRoleAssignmentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]         = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["scope"]                   = new("Scope", "scope", typeof(string), "Fully qualified scope to create the role assignment at (the grant target), e.g. a subscription, resource group, or resource ID. Mutually exclusive with --resource-group. Defaults to the subscription scope.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]          = new("ResourceGroupName", "resource-group", typeof(string), "Create the role assignment scoped to this resource group under the subscription. Mutually exclusive with --scope.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
                ["assignee"]                = new("Assignee", "assignee", typeof(string), "The principal to assign the role to. Accepts an object (principal) ID; name/UPN/service-principal resolution is not yet supported.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["assignee-object-id"]      = new("AssigneeObjectId", "assignee-object-id", typeof(string), "The principal's object (principal) ID (GUID) to assign the role to.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["role"]                    = new("RoleDefinitionId", "role", typeof(string), "The role to assign: a role definition ID (GUID) or a fully-qualified role definition ID. Role name resolution is not yet supported.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["assignee-principal-type"] = new("PrincipalType", "assignee-principal-type", typeof(string), "The principal type of the assignee (User, Group, ServicePrincipal, or ForeignGroup).", IsRequired: false, Aliases: [], AllowedValues: ["User", "Group", "ServicePrincipal", "ForeignGroup"]),
                ["name"]                    = new("Name", "name", typeof(string), "The role assignment name (a GUID). Generated when omitted.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["description"]             = new("Description", "description", typeof(string), "Description of the role assignment.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["condition"]               = new("Condition", "condition", typeof(string), "Condition under which the principal is granted permission.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["condition-version"]       = new("ConditionVersion", "condition-version", typeof(string), "Version of the condition syntax (currently only '2.0').", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["g"] = "resource-group",
                ["n"] = "name",
            }
        );
        private static readonly CommandMetadata s_newResourceGroupDeployment_CLI = new(
            Name: "NewResourceGroupDeploymentProcessor_Orchestrator",
            InvocationName: "deployment group create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]  = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]   = new("ResourceGroupName", "resource-group", typeof(string), "The name of the resource group to deploy into.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]             = new("Name", "name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["template-uri"]     = new("TemplateUri", "template-uri", typeof(string), "The URI of the ARM template to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-file"]    = new("TemplateFile", "template-file", typeof(string), "The path to a local ARM template file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-spec-id"] = new("TemplateSpecId", "template-spec-id", typeof(string), "The resource id of a Template Spec to deploy.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["query-string"]     = new("QueryString", "query-string", typeof(string), "The query string (such as a SAS token) to use with the template URI and/or the parameters URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"]   = new("ParametersUri", "parameters-uri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"]  = new("ParametersFile", "parameters-file", typeof(string), "The path to a local parameters file to deploy inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["mode"]             = new("Mode", "mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["parameter"]        = new("Parameter", "parameter", typeof(string[]), "Inline deployment parameter(s) in KEY=VALUE form (repeatable). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rollback-on-error"] = new("RollbackOnError", "rollback-on-error", typeof(bool), "Roll the resource group back to the last successful deployment on error.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["rollback-to-deployment-name"] = new("RollbackToDeploymentName", "rollback-to-deployment-name", typeof(string), "Roll the resource group back to a specific named deployment on error. Mutually exclusive with rollback-on-error.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "The validation level of the deployment.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_rest_CLI = new(
            Name: "RestProcessor_Orchestrator",
            InvocationName: "rest",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.RestProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.RestProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["method"]  = new("Method", "method", typeof(string), "HTTP method (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS). Defaults to GET.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["uri"]     = new("Uri", "uri", typeof(string), "Absolute URL or relative ARM path. Relative paths are prefixed with https://management.azure.com. The token {subscriptionId} is replaced with the current subscription. Mutually exclusive with the piece-wise ByParameters flags.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["body"]    = new("Payload", "body", typeof(string), "Optional request body (typically JSON); sent whenever provided, for any method.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["headers"] = new("Headers", "headers", typeof(List<string>), "Optional request headers in KEY=VALUE form (space-separated).", IsRequired: false, Aliases: [], AllowedValues: []),
                ["subscription"]      = new("SubscriptionId", "subscription", typeof(string), "Subscription id used to build an ARM URL from piece-wise flags. Defaults to the current subscription. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]    = new("ResourceGroupName", "resource-group", typeof(string), "Resource group name segment for a piece-wise ARM URL. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-provider"] = new("ResourceProviderName", "resource-provider", typeof(string), "Resource provider namespace (e.g. Microsoft.Resources) for a piece-wise ARM URL. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-type"]     = new("ResourceType", "resource-type", typeof(List<string>), "Resource type segments (space-separated), interleaved with --name, for a piece-wise ARM URL. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]              = new("Name", "name", typeof(List<string>), "Resource name segments (space-separated), interleaved with --resource-type (may have one fewer entry for a collection GET). Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["api-version"]       = new("ApiVersion", "api-version", typeof(string), "api-version for a piece-wise ARM URL. Required when building from flags. Mutually exclusive with --uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["uri-parameters"]    = new("UriParameters", "uri-parameters", typeof(List<string>), "Optional query-string parameters in KEY=VALUE form (space-separated), appended to the final URL. Allowed in both --uri and ByParameters modes.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_newResourceGroupDeploymentWhatIf_CLI = new(
            Name: "NewResourceGroupDeploymentWhatIfProcessor_Orchestrator",
            InvocationName: "deployment group what-if",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupDeploymentWhatIfProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewResourceGroupDeploymentWhatIfProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"]  = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["resource-group"]   = new("ResourceGroupName", "resource-group", typeof(string), "The name of the resource group to run the what-if against.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"]             = new("Name", "name", typeof(string), "The name of the deployment.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["template-uri"]     = new("TemplateUri", "template-uri", typeof(string), "The URI of the ARM template to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-file"]    = new("TemplateFile", "template-file", typeof(string), "The path to a local ARM template file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-spec-id"] = new("TemplateSpecId", "template-spec-id", typeof(string), "The resource id of a Template Spec to evaluate.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["query-string"]     = new("QueryString", "query-string", typeof(string), "The query string (such as a SAS token) to use with the template URI and/or the parameters URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"]   = new("ParametersUri", "parameters-uri", typeof(string), "The URI of a parameters file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"]  = new("ParametersFile", "parameters-file", typeof(string), "The path to a local parameters file to evaluate inline.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["mode"]             = new("Mode", "mode", typeof(string), "The deployment mode. Incremental (default) or Complete.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]),
                ["parameter"]        = new("Parameter", "parameter", typeof(string[]), "Inline deployment parameter(s) in KEY=VALUE form (repeatable). Values are parsed as JSON when possible (count=3 a number, flag=true a bool), else treated as strings; quote to force a string (e.g. zip=\"01234\"). Overlay same-named keys from a parameters file. Cannot be combined with parameters-uri.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "The validation level of the what-if operation.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
                ["result-format"]    = new("ResultFormat", "result-format", typeof(string), "The format of the what-if results. FullResourcePayloads (default) or ResourceIdOnly.", IsRequired: false, Aliases: [], AllowedValues: ["FullResourcePayloads", "ResourceIdOnly"]),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // `includeMode` matches upstream `az deployment mg`: only `create` exposes `--mode`.
        // `validate` and `what-if` take no `mode` argument and always behave as Incremental
        // (see `validate_arm_template_at_management_group`/`what_if_deploy_arm_template_at_management_group`).
        private static Dictionary<string, ParameterMetadata> CreateManagementDeploymentBodyCliParameters(bool includeMode = true)
        {
            Dictionary<string, ParameterMetadata> parameters = new()
            {
                ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"] = new("Name", "name", typeof(string), "The deployment name. Derived when omitted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["location"] = new("Location", "location", typeof(string), "The location used to store deployment metadata.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["template-file"] = new("TemplateFile", "template-file", typeof(string), "A local JSON or Bicep template file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-uri"] = new("TemplateUri", "template-uri", typeof(string), "The template URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["template-spec-id"] = new("TemplateSpecId", "template-spec-id", typeof(string), "The template spec resource ID.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["query-string"] = new("QueryString", "query-string", typeof(string), "A query string for linked template or parameter URIs.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-file"] = new("ParametersFile", "parameters-file", typeof(string), "A local JSON or bicepparam file.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters-uri"] = new("ParametersUri", "parameters-uri", typeof(string), "The parameter file URI.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["parameters"] = new("Parameter", "parameters", typeof(string[]), "A parameter source or repeated KEY=VALUE overlays.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["validation-level"] = new("ValidationLevel", "validation-level", typeof(string), "The validation level.", IsRequired: false, Aliases: [], AllowedValues: ["Template", "Provider", "ProviderNoRbac"]),
            };
            if (includeMode)
            {
                parameters["mode"] = new("Mode", "mode", typeof(string), "The deployment mode.", IsRequired: false, Aliases: [], AllowedValues: ["Incremental", "Complete"]);
            }
            return parameters;
        }
        private static readonly CommandMetadata s_newManagementGroupDeployment_CLI = new(
            Name: "NewManagementGroupDeploymentProcessor_Orchestrator",
            InvocationName: "deployment mg create",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewManagementGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.NewManagementGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>(CreateManagementDeploymentBodyCliParameters())
            {
                ["what-if"] = new("WhatIf", "what-if", typeof(bool), "Preview changes without creating the deployment.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["confirm-with-what-if"] = new("ConfirmWithWhatIf", "confirm-with-what-if", typeof(bool), "Preview changes and ask before creating the deployment.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["proceed-if-no-change"] = new("ProceedIfNoChange", "proceed-if-no-change", typeof(bool), "Skip confirmation when what-if reports no changes.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["what-if-result-format"] = new("WhatIfResultFormat", "what-if-result-format", typeof(string), "The what-if result format.", IsRequired: false, Aliases: [], AllowedValues: ["FullResourcePayloads", "ResourceIdOnly"]),
                ["what-if-exclude-change-types"] = new("WhatIfExcludeChangeType", "what-if-exclude-change-types", typeof(string[]), "What-if change types to exclude.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_validateManagementGroupDeployment_CLI = new(
            Name: "ValidateManagementGroupDeploymentProcessor_Orchestrator",
            InvocationName: "deployment mg validate",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ValidateManagementGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ValidateManagementGroupDeploymentProcessor_Orchestrator),
            Parameters: CreateManagementDeploymentBodyCliParameters(includeMode: false),
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_whatIfManagementGroupDeployment_CLI = new(
            Name: "ManagementGroupDeploymentWhatIfProcessor_Orchestrator",
            InvocationName: "deployment mg what-if",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ManagementGroupDeploymentWhatIfProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ManagementGroupDeploymentWhatIfProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>(CreateManagementDeploymentBodyCliParameters(includeMode: false))
            {
                ["result-format"] = new("ResultFormat", "result-format", typeof(string), "The what-if result format.", IsRequired: false, Aliases: [], AllowedValues: ["FullResourcePayloads", "ResourceIdOnly"]),
                ["exclude-change-types"] = new("ExcludeChangeTypes", "exclude-change-types", typeof(string[]), "What-if change types to exclude.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_listManagementGroupDeployment_CLI = new(
            Name: "ListAtManagementGroupScopeDeploymentsProcessor",
            InvocationName: "deployment mg list",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListAtManagementGroupScopeDeploymentsProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ListAtManagementGroupScopeDeploymentsProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("GroupId", "management-group-id", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["filter"] = new("_filter", "filter", typeof(string), "Filter deployments by provisioning state.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getManagementGroupDeployment_CLI = new(
            Name: "GetAtManagementGroupScopeDeploymentsProcessor",
            InvocationName: "deployment mg show",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetAtManagementGroupScopeDeploymentsProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.GetAtManagementGroupScopeDeploymentsProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("GroupId", "management-group-id", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"] = new("DeploymentName", "name", typeof(string), "The deployment name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_cancelManagementGroupDeployment_CLI = new(
            Name: "StopManagementGroupDeploymentProcessor_Orchestrator",
            InvocationName: "deployment mg cancel",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.StopManagementGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.StopManagementGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"] = new("Name", "name", typeof(string), "The deployment name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_deleteManagementGroupDeployment_CLI = new(
            Name: "DeleteAtManagementGroupScopeDeploymentsProcessor",
            InvocationName: "deployment mg delete",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeleteAtManagementGroupScopeDeploymentsProcessor",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.DeleteAtManagementGroupScopeDeploymentsProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("GroupId", "management-group-id", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"] = new("DeploymentName", "name", typeof(string), "The deployment name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_exportManagementGroupDeployment_CLI = new(
            Name: "ExportTemplateAtManagementGroupScopeDeploymentsProcessor_Customization",
            InvocationName: "deployment mg export",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ExportTemplateAtManagementGroupScopeDeploymentsProcessor_Customization",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.ExportTemplateAtManagementGroupScopeDeploymentsProcessor_Customization),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("GroupId", "management-group-id", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"] = new("DeploymentName", "name", typeof(string), "The deployment name.", IsRequired: true, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_waitManagementGroupDeployment_CLI = new(
            Name: "WaitManagementGroupDeploymentProcessor_Orchestrator",
            InvocationName: "deployment mg wait",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Resources.Processors.WaitManagementGroupDeploymentProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Resources",
            ProcessorType: typeof(Processors.WaitManagementGroupDeploymentProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["management-group-id"] = new("ManagementGroupId", "management-group-id", typeof(string), "The management group ID.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["name"] = new("Name", "name", typeof(string), "The deployment name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["created"] = new("Created", "created", typeof(bool), "Wait until provisioning succeeds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["deleted"] = new("Deleted", "deleted", typeof(bool), "Wait until the deployment is deleted.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["exists"] = new("Exists", "exists", typeof(bool), "Wait until the deployment exists.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["updated"] = new("Updated", "updated", typeof(bool), "Wait until provisioning succeeds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["custom"] = new("Custom", "custom", typeof(string), "Wait until a JMESPath expression evaluates to true.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["interval"] = new("Interval", "interval", typeof(int), "Polling interval in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["timeout"] = new("Timeout", "timeout", typeof(int), "Maximum wait time in seconds.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        // ── Dispatch Tables ──
        private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
        {
            ["New-AzResourceGroup"]      = s_newResourceGroup_PS,
            ["Set-AzResourceGroup"]      = s_setResourceGroup_PS,
            ["Remove-AzResourceGroup"]   = s_removeResourceGroup_PS,
            ["Get-AzResourceGroup_Get"]  = s_getResourceGroup_PS,
            ["Get-AzResourceGroup_List"] = s_listResourceGroup_PS,
            ["Get-AzRoleAssignment"]     = s_listRoleAssignment_PS,
            ["New-AzRoleAssignment"]     = s_newRoleAssignment_PS,
            ["New-AzResourceGroupDeployment"] = s_newResourceGroupDeployment_PS,
            ["New-AzResourceGroupDeploymentWhatIf"] = s_newResourceGroupDeploymentWhatIf_PS,
            ["New-AzManagementGroupDeployment"] = s_newManagementGroupDeployment_PS,
            ["Test-AzManagementGroupDeployment"] = s_validateManagementGroupDeployment_PS,
            ["Get-AzManagementGroupDeployment_Get"] = s_getManagementGroupDeployment_PS,
            ["Get-AzManagementGroupDeployment_List"] = s_listManagementGroupDeployment_PS,
            ["Stop-AzManagementGroupDeployment"] = s_stopManagementGroupDeployment_PS,
            ["Remove-AzManagementGroupDeployment"] = s_removeManagementGroupDeployment_PS,
            ["Invoke-AzRestMethod"] = s_rest_PS,
        };
        // Full-path keys. Each key is `{verb} {path}` where `verb` matches
        // an [ExtensionVerb] declaration on this class; the dispatcher does longest-prefix match on the
        // (verb, path) tuple via Core.Extensions.LeafIndex. Resources contributes both the
        // "group" verb (resource group CRUD) and the "deployment" verb (resource-group-scoped
        // ARM/Bicep deployments).
        private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
        {
            ["group create"]              = s_newResourceGroup_CLI,
            ["group delete"]              = s_deleteResourceGroup_CLI,
            ["group exists"]              = s_existsResourceGroup_CLI,
            ["group list"]                = s_listResourceGroup_CLI,
            ["group show"]                = s_getResourceGroup_CLI,
            ["group update"]              = s_updateResourceGroup_CLI,
            ["role assignment list"]      = s_listRoleAssignment_CLI,
            ["role assignment create"]    = s_newRoleAssignment_CLI,
            ["deployment group create"]   = s_newResourceGroupDeployment_CLI,
            ["deployment group what-if"]  = s_newResourceGroupDeploymentWhatIf_CLI,
            ["deployment mg create"]      = s_newManagementGroupDeployment_CLI,
            ["deployment mg list"]        = s_listManagementGroupDeployment_CLI,
            ["deployment mg show"]        = s_getManagementGroupDeployment_CLI,
            ["deployment mg cancel"]      = s_cancelManagementGroupDeployment_CLI,
            ["deployment mg delete"]      = s_deleteManagementGroupDeployment_CLI,
            ["deployment mg export"]      = s_exportManagementGroupDeployment_CLI,
            ["deployment mg validate"]    = s_validateManagementGroupDeployment_CLI,
            ["deployment mg what-if"]     = s_whatIfManagementGroupDeployment_CLI,
            ["deployment mg wait"]        = s_waitManagementGroupDeployment_CLI,
            // rest is a special command: the command group word itself is the verb (az rest ...).
            ["rest"] = s_rest_CLI,
        };

        // Per-processor --profile / -Profile override (workaround until a global-parameter framework
        // exists). Injected into every command so Resource processors accept the parameter; the Engine
        // resolves the named profile before the processor runs.
        static ExtensionIndex()
        {
            foreach (var metadata in s_cliTable.Values)
            {
                metadata.Parameters.TryAdd("profile", new ParameterMetadata("Profile", "profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
            }
            foreach (var metadata in s_psTable.Values)
            {
                metadata.Parameters.TryAdd("Profile", new ParameterMetadata("Profile", "Profile", typeof(string), "Use the named profile for this command (overrides the active profile).", IsRequired: false, Aliases: [], AllowedValues: []));
            }
        }
        // ── Lookup ──
        public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
        {
            var table = clientType switch
            {
                ClientType.PowerShell => s_psTable,
                ClientType.CLI => s_cliTable,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
            return table.TryGetValue(name, out var metadata)
                ? metadata
                : throw new KeyNotFoundException($"Command '{name}' not found for client type '{clientType}'.");
        }
        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable,
                ClientType.CLI => s_cliTable,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }
        public IEnumerable<string> GetCommandKeys(ClientType clientType)
        {
            return clientType switch
            {
                ClientType.PowerShell => s_psTable.Keys,
                ClientType.CLI => s_cliTable.Keys,
                _ => throw new ArgumentException($"Client type '{clientType}' is not supported.")
            };
        }

        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType)
        {
            return GetCommandTable(clientType).Values;
        }
    }
}
