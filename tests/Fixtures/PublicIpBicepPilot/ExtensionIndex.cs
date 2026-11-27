// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Processors;
using NetworkIndex = Microsoft.Azure.Azclips.Extensions.Network.ExtensionIndex;

namespace Azclips.Tests.Fixtures.PublicIpBicepPilot;

public sealed class ExtensionIndex : IExtensionIndex
{
    private readonly NetworkIndex index = new();

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
    {
        CommandMetadata command = index.GetCommandMetadata(name, clientType);
        return command.ProcessorType == typeof(CreateOrUpdatePublicIPAddressesProcessor_Customization)
            ? command with { ProcessorType = typeof(PublicIpBicepCreateProcessor) }
            : command;
    }

    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) => index.GetCommandTable(clientType);
    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => index.GetAllCommands(clientType);
    public IEnumerable<string> GetCommandKeys(ClientType clientType) => index.GetCommandKeys(clientType);
}
