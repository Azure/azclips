// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources;
using Xunit;

namespace Cli.Tests.Extensions;

/// <summary>
/// The Resources extension injects <c>--profile</c> into every command's metadata until a global-parameter
/// framework exists, and the binder needs a matching public property or the command fails at run time.
/// </summary>
public class ResourcesProfileParameterTests
{
    public static TheoryData<string, ClientType> Commands()
    {
        var data = new TheoryData<string, ClientType>();
        var index = new ExtensionIndex();
        foreach (var name in index.GetCommandTable(ClientType.CLI).Keys)
        {
            data.Add(name, ClientType.CLI);
        }

        foreach (var name in index.GetCommandTable(ClientType.PowerShell).Keys)
        {
            data.Add(name, ClientType.PowerShell);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void EveryCommandThatAdvertisesProfile_HasABindableProperty(string command, ClientType clientType)
    {
        var metadata = new ExtensionIndex().GetCommandMetadata(command, clientType);
        var parameterName = clientType == ClientType.PowerShell ? "Profile" : "profile";
        Assert.True(metadata.Parameters.ContainsKey(parameterName), $"'{command}' should accept --profile.");

        var property = metadata.ProcessorType.GetProperty("Profile");
        Assert.NotNull(property);
        Assert.Equal(typeof(string), property!.PropertyType);
        Assert.True(property.CanWrite, $"'{command}' binds --profile onto a read-only property.");
    }
}
