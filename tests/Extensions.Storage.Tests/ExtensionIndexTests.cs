// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class ExtensionIndexTests
{
    [Fact]
    public void OnlyReviewedCommandsAreExposed()
    {
        var index = new ExtensionIndex();
        string[] cli =
        [
            "storage account create", "storage account show", "storage account list", "storage account delete",
            "storage account keys list", "storage blob show", "storage blob exists", "storage blob delete",
            "storage blob set-tier", "storage blob list", "storage blob copy start", "storage blob upload",
            "storage blob download", "storage blob download-batch", "storage file upload", "storage file download",
            "storage file list", "storage directory create", "storage directory list"
        ];
        string[] ps =
        [
            "New-AzStorageAccount", "Get-AzStorageAccount_AccountName", "Get-AzStorageAccount_ResourceGroup",
            "Remove-AzStorageAccount", "New-AzStorageContext", "Get-AzStorageAccountKey", "Get-AzStorageBlob", "Remove-AzStorageBlob",
            "Set-AzStorageBlobContent", "Get-AzStorageBlobContent", "Start-AzStorageBlobCopy",
            "Set-AzStorageFileContent", "Get-AzStorageFileContent", "Get-AzStorageFile", "New-AzStorageDirectory"
        ];
        Assert.Equal(cli.Order(StringComparer.Ordinal), index.GetCommandKeys(ClientType.CLI).Order(StringComparer.Ordinal));
        Assert.Equal(ps.Order(StringComparer.Ordinal), index.GetCommandKeys(ClientType.PowerShell).Order(StringComparer.Ordinal));
        Assert.Equal(typeof(ListStorageAccountKeysCommand),
            index.GetCommandMetadata("storage account keys list", ClientType.CLI).ProcessorType);
        Assert.Equal(typeof(NewStorageContextProcessor),
            index.GetCommandMetadata("New-AzStorageContext", ClientType.PowerShell).ProcessorType);
        Assert.Equal(typeof(ListStorageAccountKeysProcessor),
            index.GetCommandMetadata("Get-AzStorageAccountKey", ClientType.PowerShell).ProcessorType);
        Assert.Equal(typeof(GetStorageBlobsPowerShellCommand),
            index.GetCommandMetadata("Get-AzStorageBlob", ClientType.PowerShell).ProcessorType);
        Assert.Equal(typeof(RemoveStorageBlobPowerShellCommand),
            index.GetCommandMetadata("Remove-AzStorageBlob", ClientType.PowerShell).ProcessorType);
        Assert.Equal(typeof(StartStorageBlobCopyCommand),
            index.GetCommandMetadata("storage blob copy start", ClientType.CLI).ProcessorType);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void MetadataMapsOnlyPublicCustomizationProperties(ClientType client)
    {
        var index = new ExtensionIndex();
        foreach (var metadata in index.GetAllCommands(client)
            .Where(metadata => metadata.ProcessorType.Namespace != "Microsoft.Azure.Azclips.Extensions.Storage.Processors"))
        {
            Assert.EndsWith(".Customization", metadata.ProcessorType.Namespace);
            foreach (var parameter in metadata.Parameters.Values)
            {
                var property = metadata.ProcessorType.GetProperty(parameter.Name);
                Assert.NotNull(property);
                Assert.Equal(parameter.ParameterType, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
                Assert.True(property.SetMethod!.IsPublic);
                foreach (string alias in parameter.Aliases)
                    Assert.Equal(parameter.InvocationName, metadata.ParameterAliases[alias]);
            }
            Assert.DoesNotContain(metadata.Parameters.Values, parameter => parameter.Name is "ReturnCollection" or "Url");
            if (client == ClientType.CLI)
                Assert.DoesNotContain(metadata.Parameters.Values, parameter => parameter.Name == "Context");
        }
        Assert.Throws<KeyNotFoundException>(() => index.GetCommandMetadata("unregistered", client));
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void EveryCommandMetadataMatchesItsKeyAndProcessor(ClientType client)
    {
        foreach (var (key, metadata) in new ExtensionIndex().GetCommandTable(client))
        {
            Assert.Equal(key, metadata.InvocationName);
            Assert.Equal(metadata.ProcessorType.Name, metadata.Name);
            Assert.Equal(metadata.ProcessorType.FullName, metadata.FullName);
            Assert.Equal(metadata.ProcessorType.Assembly.GetName().Name, metadata.AssemblyName);
            foreach (var (name, parameter) in metadata.Parameters)
                Assert.Equal(name, parameter.InvocationName);
            var aliases = metadata.Parameters.Values
                .SelectMany(parameter => parameter.Aliases.Select(alias => (alias, parameter.InvocationName)))
                .ToDictionary(pair => pair.alias, pair => pair.InvocationName);
            Assert.Equal(aliases.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                metadata.ParameterAliases.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        }
    }

    [Fact]
    public void UnsupportedClientFailsExplicitly() =>
        Assert.Throws<ArgumentException>(() => new ExtensionIndex().GetCommandTable((ClientType)999));

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CliAdapterPreservesCollectionShapeAndKerberosSelection(bool kerb, bool empty)
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(kerb ? "kerb" : null, BlobWorkflowTests.Query(request)["$expand"]);
            Assert.Contains("/subscriptions/11111111-1111-1111-1111-111111111111/", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(empty ? "{\"keys\":[]}" :
                    "{\"keys\":[{\"keyName\":\"key1\",\"value\":\"a2V5\",\"permissions\":\"Full\"}]}")
            });
        });
        var processor = new ListStorageAccountKeysCommand
        {
            ResourceGroupName = "rg", AccountName = "account123",
            SubscriptionId = "11111111-1111-1111-1111-111111111111",
            ExpandKeyType = kerb ? "kerb" : null
        };
        processor.Init(fixture.Context);
        await processor.ProcessAsync();
        var keys = Assert.IsType<StorageAccountKey[]>(Assert.Single(fixture.Output));
        Assert.Equal(empty ? 0 : 1, keys.Length);
        Assert.Single(fixture.TokenRequests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("full")]
    public async Task InvalidExpansionFailsBeforeAuthentication(string expansion)
    {
        var fixture = new Fixture();
        var processor = new ListStorageAccountKeysCommand { ExpandKeyType = expansion };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task InvalidKeyResponsesProduceOneFailureDiagnosticAndNoOutput()
    {
        var fixture = new Fixture(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"keys\":null}")
        }));
        var processor = new ListStorageAccountKeysCommand
        {
            ResourceGroupName = "rg", AccountName = "account123",
            SubscriptionId = "11111111-1111-1111-1111-111111111111"
        };
        processor.Init(fixture.Context);
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync());
        Assert.Single(fixture.Messages, message => message.Id == Core.Abstractions.Runtime.Events.ResourceOperationFailed);
        Assert.Empty(fixture.Output);
    }
}
