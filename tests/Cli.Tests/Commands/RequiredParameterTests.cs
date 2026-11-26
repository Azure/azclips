// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using KeyVaultIndex = Microsoft.Azure.Azclips.Extensions.KeyVault.ExtensionIndex;
using ManagedServiceIdentityIndex = Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.ExtensionIndex;
using MsGraphIndex = Microsoft.Azure.Azclips.Extensions.MsGraph.ExtensionIndex;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cli.Tests.Commands;

/// <summary>
/// Pins the required parameters of every CLI command in <see cref="CommandParameterTests"/>. That class generates its
/// missing-parameter cases from the extension indexes, so a parameter that stops being required would drop its case
/// without failing; here it fails, and the list must be updated on purpose.
/// </summary>
public sealed class RequiredParameterTests
{
    private static readonly CommandMetadata[] s_commands =
    [
        .. new KeyVaultIndex().GetAllCommands(ClientType.CLI),
        .. new ResourcesIndex().GetAllCommands(ClientType.CLI),
        .. new MsGraphIndex().GetAllCommands(ClientType.CLI),
        .. new ManagedServiceIdentityIndex().GetAllCommands(ClientType.CLI),
    ];

    // Command and its required parameters in ordinal order.
    private static readonly (string Command, string[] Required)[] s_pinned =
    [
        // KeyVault
        ("keyvault certificate create", ["name"]),
        ("keyvault certificate delete", ["name"]),
        ("keyvault certificate download", ["file", "name"]),
        ("keyvault certificate import", ["file", "name"]),
        ("keyvault certificate list", []),
        ("keyvault certificate list-versions", ["name"]),
        ("keyvault certificate set-attributes", ["name"]),
        ("keyvault certificate show", ["name"]),
        ("keyvault secret delete", ["name"]),
        ("keyvault secret list", []),
        ("keyvault secret list-versions", ["name"]),
        ("keyvault secret set", ["name"]),
        ("keyvault secret set-attributes", ["name"]),
        ("keyvault secret show", ["name"]),

        // Resources
        ("deployment group cancel", ["name", "resource-group"]),
        ("deployment group create", ["resource-group"]),
        ("deployment group delete", ["name", "resource-group"]),
        ("deployment group export", ["name", "resource-group"]),
        ("deployment group list", ["resource-group"]),
        ("deployment group show", ["name", "resource-group"]),
        ("deployment group validate", ["resource-group"]),
        ("deployment group what-if", ["resource-group"]),
        ("deployment mg cancel", ["management-group-id", "name"]),
        ("deployment mg create", ["location", "management-group-id"]),
        ("deployment mg delete", ["management-group-id", "name"]),
        ("deployment mg export", ["management-group-id", "name"]),
        ("deployment mg list", ["management-group-id"]),
        ("deployment mg show", ["management-group-id", "name"]),
        ("deployment mg validate", ["location", "management-group-id"]),
        ("deployment mg what-if", ["location", "management-group-id"]),
        ("deployment operation group list", ["name", "resource-group"]),
        ("deployment operation group show", ["name", "operation-ids", "resource-group"]),
        ("deployment operation mg list", ["management-group-id", "name"]),
        ("deployment operation mg show", ["management-group-id", "name", "operation-ids"]),
        ("deployment operation sub list", ["name"]),
        ("deployment operation sub show", ["name", "operation-ids"]),
        ("deployment operation tenant list", ["name"]),
        ("deployment operation tenant show", ["name", "operation-ids"]),
        ("deployment sub cancel", ["name"]),
        ("deployment sub create", ["location"]),
        ("deployment sub delete", ["name"]),
        ("deployment sub export", ["name"]),
        ("deployment sub list", []),
        ("deployment sub show", ["name"]),
        ("deployment sub validate", ["location"]),
        ("deployment sub what-if", ["location"]),
        ("deployment tenant cancel", ["name"]),
        ("deployment tenant create", ["location"]),
        ("deployment tenant delete", ["name"]),
        ("deployment tenant export", ["name"]),
        ("deployment tenant list", []),
        ("deployment tenant show", ["name"]),
        ("deployment tenant validate", ["location"]),
        ("deployment tenant wait", ["name"]),
        ("deployment tenant what-if", ["location"]),
        ("group create", ["location", "name"]),
        ("group delete", ["name"]),
        ("group exists", ["name"]),
        ("group list", []),
        ("group show", ["name"]),
        ("group update", ["name"]),
        ("rest", []),
        ("role assignment create", ["role"]),
        ("role assignment delete", ["name"]),
        ("role assignment list", []),

        // MsGraph
        ("msgraph app create", ["display-name"]),
        ("msgraph app delete", ["id"]),
        ("msgraph app list", []),
        ("msgraph app show", ["id"]),
        ("msgraph group list", []),
        ("msgraph group member check", ["group", "member-id"]),
        ("msgraph group member list", []),
        ("msgraph group show", ["group"]),
        ("msgraph user list", []),
        ("msgraph user show", ["id"]),

        // ManagedServiceIdentity
        ("identity create", ["location", "name", "resource-group"]),
        ("identity delete", []),
        ("identity federated-credential create", ["identity-name", "name", "resource-group"]),
        ("identity federated-credential delete", ["identity-name", "name", "resource-group"]),
        ("identity federated-credential list", ["identity-name", "resource-group"]),
        ("identity federated-credential show", ["identity-name", "name", "resource-group"]),
        ("identity federated-credential update", ["identity-name", "name", "resource-group"]),
        ("identity list", []),
        ("identity show", []),
        ("identity update", []),
    ];

    public static TheoryData<string, string[]> GetPinnedCommands()
    {
        TheoryData<string, string[]> data = new();
        foreach ((string command, string[] required) in s_pinned)
        {
            data.Add(command, required);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GetPinnedCommands))]
    public void RequiredParameters_PinnedCommand_MatchExtensionIndex(string command, string[] pinnedRequired)
    {
        CommandMetadata metadata = s_commands.Single(c => c.InvocationName == command);

        string[] required = metadata.Parameters.Values.Where(p => p.IsRequired).Select(p => p.InvocationName).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(pinnedRequired, required);
    }

    [Fact]
    public void Commands_EveryExtensionCommand_ArePinned()
    {
        string[] pinnedCommands = s_pinned.Select(p => p.Command).Order(StringComparer.Ordinal).ToArray();

        string[] commands = s_commands.Select(c => c.InvocationName).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(pinnedCommands, commands);
    }
}