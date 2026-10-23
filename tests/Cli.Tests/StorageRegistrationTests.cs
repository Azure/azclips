// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Cli.Tests;

public sealed class StorageRegistrationTests
{
    private static readonly Profile s_profile = new()
    {
        Name = "selected", TenantId = "tenant", SubscriptionId = "11111111-1111-1111-1111-111111111111"
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AccountKeyFlagsAndAliasesBindToTheReviewedAdapter(bool aliases)
    {
        var processor = Bind(new Dictionary<string, object>
        {
            [aliases ? "g" : "resource-group"] = "rg",
            [aliases ? "n" : "account-name"] = "account123",
            ["subscription-id"] = "22222222-2222-2222-2222-222222222222",
            ["expand-key-type"] = "kerb"
        });
        Assert.Equal("rg", processor.ResourceGroupName);
        Assert.Equal("account123", processor.AccountName);
        Assert.Equal("22222222-2222-2222-2222-222222222222", processor.SubscriptionId);
        Assert.Equal("kerb", processor.ExpandKeyType);
    }

    [Fact]
    public void DefaultsUseTheSelectedSubscriptionWithoutKerberosExpansion()
    {
        var processor = Bind(new Dictionary<string, object>
        {
            ["resource-group"] = "rg", ["account-name"] = "account123"
        });
        Assert.Equal(s_profile.SubscriptionId, processor.SubscriptionId);
        Assert.Null(processor.ExpandKeyType);
    }

    [Theory]
    [InlineData("resource-group")]
    [InlineData("account-name")]
    public void RequiredAddressingIsEnforced(string missing)
    {
        var arguments = new Dictionary<string, object> { ["resource-group"] = "rg", ["account-name"] = "account123" };
        arguments.Remove(missing);
        Assert.Throws<MissingRequiredArgumentException>(() => Bind(arguments));
    }

    [Theory]
    [InlineData("")]
    [InlineData("full")]
    public void ExpansionIsRestrictedToKerberos(string expansion) =>
        Assert.Throws<UsageErrorException>(() => Bind(new Dictionary<string, object>
        {
            ["resource-group"] = "rg", ["account-name"] = "account123", ["expand-key-type"] = expansion
        }));

    [Theory]
    [InlineData("show", typeof(ShowStorageBlobCommand))]
    [InlineData("exists", typeof(TestStorageBlobExistsCommand))]
    [InlineData("delete", typeof(RemoveStorageBlobCommand))]
    [InlineData("set-tier", typeof(SetStorageBlobTierCommand))]
    public void BlobAddressAliasesAndRawTimeoutBindWithoutAnArmSubscription(string verb, Type type)
    {
        var arguments = new Dictionary<string, object>
        {
            ["c"] = "container", ["n"] = "item", ["blob-endpoint"] = "https://account123.blob.core.windows.net/",
            ["auth-mode"] = "login", ["timeout"] = new List<List<string>> { new() { "17" } }
        };
        if (verb == "set-tier") arguments["tier"] = "Hot";
        var processor = BindBlob(verb, arguments);
        Assert.Equal(type, processor.GetType());
        Assert.Equal("container", processor.Container);
        Assert.Equal("item", processor.Blob);
        Assert.Null(processor.StorageAccountName);
        Assert.Equal("https://account123.blob.core.windows.net/", processor.BlobEndpoint);
        Assert.Equal(17, processor.Timeout);
    }

    [Fact]
    public void ShowBindsBooleanSwitchAndDateConditionsWithoutGenericDateConversion()
    {
        var processor = Assert.IsType<ShowStorageBlobCommand>(BindBlob("show", new()
        {
            ["container-name"] = "container", ["name"] = "item", ["account-name"] = "account123",
            ["include-tags"] = new List<List<string>> { new() },
            ["if-modified-since"] = "2026-10-20T00:00:00Z", ["if-unmodified-since"] = "2026-10-21T00:00:00Z",
            ["if-match"] = "\"etag\"", ["version-id"] = "version", ["lease-id"] = "lease",
            ["tags-condition"] = "\"team\"='one'"
        }));
        Assert.True(processor.IncludeTags);
        Assert.Equal("2026-10-20T00:00:00Z", processor.IfModifiedSinceInput);
        Assert.Equal("2026-10-21T00:00:00Z", processor.IfUnmodifiedSinceInput);
        Assert.Equal("\"etag\"", processor.IfMatch);
        Assert.Equal("version", processor.VersionId);
        Assert.Equal("lease", processor.LeaseId);
        Assert.Equal("\"team\"='one'", processor.IfTags);
    }

    [Fact]
    public void TierDefaultsAndAcceptedAliasesAreBound()
    {
        var defaults = Assert.IsType<SetStorageBlobTierCommand>(BindBlob("set-tier", new()
        {
            ["container-name"] = "container", ["name"] = "item", ["account-name"] = "account123", ["tier"] = "Hot"
        }));
        Assert.Equal("block", defaults.BlobType);
        var explicitType = Assert.IsType<SetStorageBlobTierCommand>(BindBlob("set-tier", new()
        {
            ["container-name"] = "container", ["name"] = "item", ["account-name"] = "account123",
            ["tier"] = "Cool", ["t"] = "block", ["r"] = "High"
        }));
        Assert.Equal("block", explicitType.BlobType);
        Assert.Equal("High", explicitType.RehydratePriority);
    }

    [Theory]
    [InlineData("if-match")]
    [InlineData("if-modified-since")]
    public void TierDoesNotExposeUnsupportedPreconditions(string condition) =>
        Assert.Throws<UsageErrorException>(() => BindBlob("set-tier", new()
        {
            ["container-name"] = "container", ["name"] = "item", ["account-name"] = "account123",
            ["tier"] = "Hot", [condition] = "unsupported"
        }));

    [Theory]
    [InlineData("auth-mode", "key")]
    [InlineData("delete-snapshots", "invalid")]
    public void UnsupportedBlobModesAreRejectedByTheCliBinder(string flag, string value) =>
        Assert.Throws<UsageErrorException>(() => BindBlob("delete", new()
        {
            ["container-name"] = "container", ["name"] = "item", ["account-name"] = "account123", [flag] = value
        }));

    [Theory]
    [InlineData("container-name")]
    [InlineData("name")]
    public void RequiredBlobAddressingIsEnforced(string missing)
    {
        var arguments = new Dictionary<string, object>
        {
            ["container-name"] = "container", ["name"] = "item", ["account-name"] = "account123"
        };
        arguments.Remove(missing);
        Assert.Throws<MissingRequiredArgumentException>(() => BindBlob("show", arguments));
    }

    private static BlobCliCommandBase BindBlob(string verb, Dictionary<string, object> arguments)
    {
        var metadata = new ExtensionIndex().GetCommandMetadata($"storage blob {verb}", ClientType.CLI);
        var logging = new ScopedLoggingService();
        var binder = new ArgumentParsingService(new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(logging));
        var processor = Assert.IsAssignableFrom<BlobCliCommandBase>(Activator.CreateInstance(metadata.ProcessorType));
        binder.BindArgumentsToProcessor(processor, arguments, metadata.Parameters, metadata.ParameterAliases,
            new Profile { Name = "selected", TenantId = "tenant" });
        return processor;
    }

    [Fact]
    public void BlobListDefaultsDoNotInheritUnlimitedOrPowerShellCountSemantics()
    {
        var processor = BindList(new() { ["c"] = "container", ["account-name"] = "account123" });
        Assert.Equal("container", processor.Container);
        Assert.Equal("5000", processor.NumResults);
        Assert.Equal("login", processor.AuthMode);
        Assert.False(processor.ShowNextMarker);
        Assert.Null(processor.Include);
        var parameters = new ExtensionIndex().GetCommandMetadata("storage blob list", ClientType.CLI).Parameters;
        Assert.False(parameters.ContainsKey("name"));
        Assert.False(parameters.ContainsKey("max-count"));
        Assert.False(parameters.ContainsKey("continuation-token"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("9223372036854775807")]
    public void BlobListBindsInt64OrAllAsStringsAndTheMarkerSwitchAsBoolean(string limit)
    {
        var processor = BindList(new()
        {
            ["container-name"] = "container", ["blob-endpoint"] = "https://account123.blob.core.windows.net/",
            ["num-results"] = new List<List<string>> { new() { limit } },
            ["include"] = "mct", ["show-next-marker"] = new List<List<string>> { new() },
            ["prefix"] = "literal*", ["delimiter"] = "/", ["marker"] = "opaque+/=&",
            ["timeout"] = new List<List<string>> { new() { "17" } }, ["profile"] = "selected"
        });
        Assert.Equal(limit, processor.NumResults);
        Assert.Equal("mct", processor.Include);
        Assert.True(processor.ShowNextMarker);
        Assert.Equal("literal*", processor.Prefix);
        Assert.Equal("/", processor.Delimiter);
        Assert.Equal("opaque+/=&", processor.Marker);
        Assert.Equal(17, processor.Timeout);
        Assert.Equal("selected", processor.Profile);
        Assert.Null(processor.StorageAccountName);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("if-match")]
    [InlineData("max-count")]
    public void BlobListDoesNotAcceptTargetConditionsOrPowerShellOnlyInputs(string unsupported) =>
        Assert.Throws<UsageErrorException>(() => BindList(new()
        {
            ["container-name"] = "container", ["account-name"] = "account123", [unsupported] = "value"
        }));

    [Fact]
    public void BlobListRequiresAContainerAtBindingTime() =>
        Assert.Throws<MissingRequiredArgumentException>(() => BindList(new() { ["account-name"] = "account123" }));

    private static ListStorageBlobsCommand BindList(Dictionary<string, object> arguments)
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("storage blob list", ClientType.CLI);
        Assert.Equal(typeof(ListStorageBlobsCommand), metadata.ProcessorType);
        var logging = new ScopedLoggingService();
        var binder = new ArgumentParsingService(new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(logging));
        var processor = new ListStorageBlobsCommand();
        binder.BindArgumentsToProcessor(processor, arguments, metadata.Parameters, metadata.ParameterAliases,
            new Profile { Name = "selected", TenantId = "tenant" });
        return processor;
    }

    private static ListStorageAccountKeysCommand Bind(Dictionary<string, object> arguments)
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("storage account keys list", ClientType.CLI);
        var logging = new ScopedLoggingService();
        var binder = new ArgumentParsingService(new CLIArgumentParsingProvider(new PropertyParser(logging), logging),
            new ProfileService(logging));
        var processor = new ListStorageAccountKeysCommand();
        binder.BindArgumentsToProcessor(processor, arguments, metadata.Parameters, metadata.ParameterAliases, s_profile);
        return processor;
    }

    [Fact]
    public void CopyAliasesAndAllConditionFamiliesBindWithoutDateTypeConversion()
    {
        var processor = Assert.IsType<StartStorageBlobCopyCommand>(BindBlob("copy start", new()
        {
            ["c"] = "dest", ["b"] = "target", ["u"] = "https://sourceaccount.blob.core.windows.net/container/blob",
            ["blob-endpoint"] = "https://account123.blob.core.windows.net/", ["source-sas"] = "sv=2026-06-06&sp=r&sig=private-test",
            ["source-account-name"] = "sourceaccount", ["source-snapshot"] = "snapshot",
            ["destination-lease-id"] = "lease", ["destination-if-match"] = "\"dest\"",
            ["destination-if-none-match"] = "\"other\"", ["destination-if-modified-since"] = "2026-10-20",
            ["destination-if-unmodified-since"] = "2026-10-21", ["destination-tags-condition"] = "\"team\"='one'",
            ["source-if-match"] = "\"source\"", ["source-if-none-match"] = "\"source-other\"",
            ["source-if-modified-since"] = "2026-10-20", ["source-if-unmodified-since"] = "2026-10-21",
            ["source-tags-condition"] = "\"team\"='two'", ["timeout"] = new List<List<string>> { new() { "9" } },
            ["metadata"] = new List<List<string>> { new() { "one=two", "three=four=five" } },
            ["tags"] = new List<List<string>> { new() { "empty", "team=one" } },
            ["tier"] = "Cool", ["rehydrate-priority"] = "High"
        }));
        Assert.Equal("dest", processor.Container); Assert.Equal("target", processor.Blob);
        Assert.Equal("sourceaccount", processor.SourceStorageAccountName);
        Assert.Equal("snapshot", processor.SourceSnapshot);
        Assert.Equal("lease", processor.LeaseId);
        Assert.Equal("\"dest\"", processor.IfMatch); Assert.Equal("\"other\"", processor.IfNoneMatch);
        Assert.Equal("2026-10-20", processor.IfModifiedSinceInput); Assert.Equal("2026-10-21", processor.IfUnmodifiedSinceInput);
        Assert.Equal("\"team\"='one'", processor.IfTags);
        Assert.Equal("\"source\"", processor.SourceIfMatch); Assert.Equal("\"source-other\"", processor.SourceIfNoneMatch);
        Assert.Equal("2026-10-20", processor.SourceIfModifiedSinceInput); Assert.Equal("2026-10-21", processor.SourceIfUnmodifiedSinceInput);
        Assert.Equal("\"team\"='two'", processor.SourceIfTags);
        Assert.Equal(["one=two", "three=four=five"], Assert.IsType<string[]>(processor.MetadataInput));
        Assert.Equal(["empty", "team=one"], Assert.IsType<string[]>(processor.TagsInput));
        Assert.Equal("Cool", processor.Tier); Assert.Equal("High", processor.RehydratePriority);
        Assert.Equal(9, processor.Timeout);
    }

    [Fact]
    public void CopyFileSourceAndExplicitIntentSwitchBindWithoutNativeInputs()
    {
        var processor = Assert.IsType<StartStorageBlobCopyCommand>(BindBlob("copy start", new()
        {
            ["destination-container"] = "dest", ["destination-blob"] = "target", ["account-name"] = "account123",
            ["source-share"] = "share", ["source-path"] = "dir/file", ["source-share-snapshot"] = "snapshot",
            ["enable-source-file-backup-request-intent"] = new List<List<string>> { new() }
        }));
        Assert.Equal("share", processor.SourceShareName);
        Assert.Equal("dir/file", processor.SourcePath);
        Assert.Equal("snapshot", processor.SourceShareSnapshot);
        Assert.True(processor.EnableSourceFileBackupRequestIntent);
        Assert.Null(processor.SourceStorageAccountName);
        Assert.Null(processor.SourceSas);
        Assert.Equal("login", processor.AuthMode);
    }

    [Theory]
    [InlineData("destination-container")]
    [InlineData("destination-blob")]
    public void CopyRequiresDestinationNamesAtBindingTime(string missing)
    {
        var arguments = new Dictionary<string, object>
        {
            ["destination-container"] = "dest", ["destination-blob"] = "target", ["account-name"] = "account123"
        };
        arguments.Remove(missing);
        Assert.Throws<MissingRequiredArgumentException>(() => BindBlob("copy start", arguments));
    }

    [Fact]
    public void FileCopySourceKeyAndExplicitLookupOptionsBindWithoutDestinationKeyAuthentication()
    {
        var processor = Assert.IsType<StartStorageBlobCopyCommand>(BindBlob("copy start", new()
        {
            ["destination-container"] = "dest", ["destination-blob"] = "target", ["account-name"] = "account123",
            ["source-share"] = "share", ["source-path"] = "file", ["source-account-key"] = "provided-key",
            ["use-source-account-key-lookup"] = new List<List<string>> { new() },
            ["source-key-resource-group"] = "source-rg", ["source-key-subscription-id"] = s_profile.SubscriptionId,
            ["source-key-name"] = "key2", ["source-sas"] = "sv=2026-06-06&sp=r&sig=provided"
        }));
        Assert.Equal("provided-key", processor.SourceAccountKey);
        Assert.True(processor.UseSourceAccountKeyLookup);
        Assert.Equal("source-rg", processor.SourceKeyResourceGroup);
        Assert.Equal(s_profile.SubscriptionId, processor.SourceKeySubscriptionId);
        Assert.Equal("key2", processor.SourceKeyName);
        Assert.NotNull(processor.SourceSas);
        Assert.Equal("login", processor.AuthMode);
    }

    [Theory]
    [InlineData("requires-sync")]
    [InlineData("destination-blob-type")]
    [InlineData("source-lease-id")]
    [InlineData("source-connection-string")]
    [InlineData("account-key")]
    [InlineData("sas-token")]
    [InlineData("overwrite")]
    [InlineData("snapshot")]
    [InlineData("container-name")]
    public void CopyDoesNotExposeExcludedOrMisleadingInputs(string flag) =>
        Assert.Throws<UsageErrorException>(() => BindBlob("copy start", new()
        {
            ["destination-container"] = "dest", ["destination-blob"] = "target", [flag] = "unsupported"
        }));

    [Theory]
    [InlineData("metadata")]
    [InlineData("tags")]
    public void EmptyCopyKeyValueOccurrencesAreRejectedByTheExistingCollectionBinder(string flag) =>
        Assert.Throws<UsageErrorException>(() => BindBlob("copy start", new()
        {
            ["destination-container"] = "dest", ["destination-blob"] = "target",
            [flag] = new List<List<string>> { new() }
        }));
}
