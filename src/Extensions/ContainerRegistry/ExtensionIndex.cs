// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry;

[ExtensionVerb("acr")]
public class ExtensionIndex : IExtensionIndex
{
    // ── PS Command Metadata ──

    private static readonly CommandMetadata s_connect_PS = new(
        Name: "ConnectAcrOrchestrator",
        InvocationName: "Connect-AzContainerRegistry",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators.ConnectAcrOrchestrator",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(ConnectAcrOrchestrator),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["Name"] = new("Name", "Name", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_listRepository_PS = new(
        Name: "ListRepositoryProcessor",
        InvocationName: "Get-AzContainerRegistryRepository_List",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.ListRepositoryProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(ListRepositoryProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["RegistryName"] = new("RegistryName", "RegistryName", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_getRepository_PS = new(
        Name: "GetRepositoryProcessor",
        InvocationName: "Get-AzContainerRegistryRepository_Get",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.GetRepositoryProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(GetRepositoryProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["RegistryName"] = new("RegistryName", "RegistryName", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Repository"]   = new("Repository", "Repository", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_removeRepository_PS = new(
        Name: "RemoveRepositoryProcessor",
        InvocationName: "Remove-AzContainerRegistryRepository",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.RemoveRepositoryProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(RemoveRepositoryProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["RegistryName"] = new("RegistryName", "RegistryName", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Repository"]   = new("Repository", "Repository", typeof(string), "The name of the repository to delete.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_listManifest_PS = new(
        Name: "ListManifestProcessor",
        InvocationName: "Get-AzContainerRegistryManifest_List",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.ListManifestProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(ListManifestProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["RegistryName"]   = new("RegistryName", "RegistryName", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["RepositoryName"] = new("RepositoryName", "RepositoryName", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_getManifest_PS = new(
        Name: "GetManifestProcessor",
        InvocationName: "Get-AzContainerRegistryManifest_Get",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.GetManifestProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(GetManifestProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["RegistryName"]   = new("RegistryName", "RegistryName", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["RepositoryName"] = new("RepositoryName", "RepositoryName", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Reference"]      = new("Reference", "Reference", typeof(string), "The manifest reference (tag or digest).", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_removeManifest_PS = new(
        Name: "RemoveManifestProcessor",
        InvocationName: "Remove-AzContainerRegistryManifest",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.RemoveManifestProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(RemoveManifestProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["RegistryName"]   = new("RegistryName", "RegistryName", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["RepositoryName"] = new("RepositoryName", "RepositoryName", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Reference"]      = new("Reference", "Reference", typeof(string), "The manifest reference (tag or digest).", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_listTag_PS = new(
        Name: "ListTagProcessor",
        InvocationName: "Get-AzContainerRegistryTag_List",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.ListTagProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(ListTagProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["RegistryName"]   = new("RegistryName", "RegistryName", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["RepositoryName"] = new("RepositoryName", "RepositoryName", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_getTag_PS = new(
        Name: "GetTagProcessor",
        InvocationName: "Get-AzContainerRegistryTag_Get",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.GetTagProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(GetTagProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["RegistryName"]   = new("RegistryName", "RegistryName", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["RepositoryName"] = new("RepositoryName", "RepositoryName", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Tag"]            = new("Tag", "Tag", typeof(string), "The tag name.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_removeTag_PS = new(
        Name: "RemoveTagProcessor",
        InvocationName: "Remove-AzContainerRegistryTag",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.RemoveTagProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(RemoveTagProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["RegistryName"]   = new("RegistryName", "RegistryName", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["RepositoryName"] = new("RepositoryName", "RepositoryName", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["Tag"]            = new("Tag", "Tag", typeof(string), "The tag name to delete.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    // ── CLI Command Metadata ──

    private static readonly CommandMetadata s_connect_CLI = new(
        Name: "ConnectAcrOrchestrator",
        InvocationName: "acr connect",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators.ConnectAcrOrchestrator",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(ConnectAcrOrchestrator),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["name"] = new("Name", "name", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_listRepository_CLI = new(
        Name: "ListRepositoryProcessor",
        InvocationName: "acr repository list",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.ListRepositoryProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(ListRepositoryProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["registry"] = new("RegistryName", "registry", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_getRepository_CLI = new(
        Name: "GetRepositoryProcessor",
        InvocationName: "acr repository show",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.GetRepositoryProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(GetRepositoryProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["registry"]   = new("RegistryName", "registry", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["repository"] = new("Repository", "repository", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_removeRepository_CLI = new(
        Name: "RemoveRepositoryProcessor",
        InvocationName: "acr repository delete",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.RemoveRepositoryProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(RemoveRepositoryProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["registry"]   = new("RegistryName", "registry", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["repository"] = new("Repository", "repository", typeof(string), "The name of the repository to delete.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_listManifest_CLI = new(
        Name: "ListManifestProcessor",
        InvocationName: "acr manifest list",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.ListManifestProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(ListManifestProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["registry"]   = new("RegistryName", "registry", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["repository"] = new("RepositoryName", "repository", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_getManifest_CLI = new(
        Name: "GetManifestProcessor",
        InvocationName: "acr manifest show",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.GetManifestProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(GetManifestProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["registry"]   = new("RegistryName", "registry", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["repository"] = new("RepositoryName", "repository", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["reference"]  = new("Reference", "reference", typeof(string), "The manifest reference (tag or digest).", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_removeManifest_CLI = new(
        Name: "RemoveManifestProcessor",
        InvocationName: "acr manifest delete",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.RemoveManifestProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(RemoveManifestProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["registry"]   = new("RegistryName", "registry", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["repository"] = new("RepositoryName", "repository", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["reference"]  = new("Reference", "reference", typeof(string), "The manifest reference (tag or digest).", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_listTag_CLI = new(
        Name: "ListTagProcessor",
        InvocationName: "acr tag list",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.ListTagProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(ListTagProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["registry"]   = new("RegistryName", "registry", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["repository"] = new("RepositoryName", "repository", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_getTag_CLI = new(
        Name: "GetTagProcessor",
        InvocationName: "acr tag show",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.GetTagProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(GetTagProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["registry"]   = new("RegistryName", "registry", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["repository"] = new("RepositoryName", "repository", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["tag"]        = new("Tag", "tag", typeof(string), "The tag name.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    private static readonly CommandMetadata s_removeTag_CLI = new(
        Name: "RemoveTagProcessor",
        InvocationName: "acr tag delete",
        Aliases: [],
        FullName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors.RemoveTagProcessor",
        AssemblyName: "Microsoft.Azure.Azclips.Extensions.ContainerRegistry",
        ProcessorType: typeof(RemoveTagProcessor),
        Parameters: new Dictionary<string, ParameterMetadata>
        {
            ["registry"]   = new("RegistryName", "registry", typeof(string), "The name of the container registry.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["repository"] = new("RepositoryName", "repository", typeof(string), "The name of the repository.", IsRequired: true, Aliases: [], AllowedValues: []),
            ["tag"]        = new("Tag", "tag", typeof(string), "The tag name to delete.", IsRequired: true, Aliases: [], AllowedValues: []),
        },
        ParameterAliases: new Dictionary<string, string>()
    );

    // ── Dispatch Tables ──

    private static readonly Dictionary<string, CommandMetadata> s_psTable = new()
    {
        ["Connect-AzContainerRegistry"]             = s_connect_PS,
        ["Get-AzContainerRegistryRepository_List"]  = s_listRepository_PS,
        ["Get-AzContainerRegistryRepository_Get"]   = s_getRepository_PS,
        ["Remove-AzContainerRegistryRepository"]    = s_removeRepository_PS,
        ["Get-AzContainerRegistryManifest_List"]    = s_listManifest_PS,
        ["Get-AzContainerRegistryManifest_Get"]     = s_getManifest_PS,
        ["Remove-AzContainerRegistryManifest"]      = s_removeManifest_PS,
        ["Get-AzContainerRegistryTag_List"]         = s_listTag_PS,
        ["Get-AzContainerRegistryTag_Get"]          = s_getTag_PS,
        ["Remove-AzContainerRegistryTag"]           = s_removeTag_PS,
    };

    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
    {
        ["acr connect"]           = s_connect_CLI,
        ["acr repository list"]   = s_listRepository_CLI,
        ["acr repository show"]   = s_getRepository_CLI,
        ["acr repository delete"] = s_removeRepository_CLI,
        ["acr manifest list"]     = s_listManifest_CLI,
        ["acr manifest show"]     = s_getManifest_CLI,
        ["acr manifest delete"]   = s_removeManifest_CLI,
        ["acr tag list"]          = s_listTag_CLI,
        ["acr tag show"]          = s_getTag_CLI,
        ["acr tag delete"]        = s_removeTag_CLI,
    };

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
