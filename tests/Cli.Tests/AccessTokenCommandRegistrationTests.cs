// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Login;
using Microsoft.Azure.Azclips.Extensions.Login.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Registration tests for the token-retrieval command. The engine binds parsed arguments onto the
/// processor by each parameter's metadata <see cref="ParameterMetadata.Name"/>, so every registered
/// parameter Name must correspond to a real writable property on the processor. A mismatch compiles
/// fine but fails at runtime with "property does not exist" — these tests guard that class of bug on
/// both the CLI and PowerShell surfaces.
/// </summary>
public class AccessTokenCommandRegistrationTests
{
    [Theory]
    [InlineData(ClientType.CLI, "account get-access-token")]
    [InlineData(ClientType.PowerShell, "Get-AzAccessToken")]
    public void GetAccessToken_IsRegisteredForSurface(ClientType surface, string invocationName)
    {
        var command = new ExtensionIndex().GetCommandMetadata(invocationName, surface);
        Assert.Equal(typeof(GetAccessTokenProcessor), command.ProcessorType);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void GetAccessToken_EveryParameterNameMapsToWritableProcessorProperty(ClientType surface)
    {
        var command = new ExtensionIndex().GetAllCommands(surface)
            .Single(c => c.ProcessorType == typeof(GetAccessTokenProcessor));

        foreach (var parameter in command.Parameters.Values)
        {
            var property = typeof(GetAccessTokenProcessor).GetProperty(parameter.Name);
            Assert.True(
                property is { CanWrite: true },
                $"Parameter metadata Name '{parameter.Name}' ({surface}) does not map to a writable property on {nameof(GetAccessTokenProcessor)}.");
        }
    }

    [Fact]
    public void GetAccessToken_PowerShellSurface_DoesNotExposePlaintextFlag()
    {
        var command = new ExtensionIndex().GetCommandMetadata("Get-AzAccessToken", ClientType.PowerShell);
        Assert.DoesNotContain(command.Parameters.Values, p => p.Name == nameof(GetAccessTokenProcessor.Plaintext));
    }
}
