// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Cli;
using Core.Cli.Output;
using Core.Runtime;
using Core.Runtime.Persistence;
using Microsoft.Azure.Azclips.Extensions.Storage;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Cli.Tests;

public sealed class StorageTransferRegistrationTests
{
    [Fact]
    public void UploadBindsFilenameDefaultTypeSettingsAndIntegralControls()
    {
        var command = Assert.IsType<UploadStorageBlobCommand>(Bind("upload", new()
        {
            ["f"] = "data.vhd", ["c"] = "content", ["account-name"] = "fixture",
            ["max-connections"] = "4", ["chunk-size"] = "512",
            ["maxsize-condition"] = "5368709120", ["content-type"] = "text/plain",
            ["content-cache-control"] = "no-cache", ["content-md5"] = "AAAAAAAAAAAAAAAAAAAAAA==",
            ["metadata"] = new List<List<string>> { new() { "a=b=c" } },
            ["tags"] = new List<List<string>> { new() { "empty" } },
            ["overwrite"] = "true", ["no-progress"] = "true"
        }));
        Assert.Null(command.Blob);
        Assert.Null(command.BlobType);
        Assert.Equal("data.vhd", command.File);
        Assert.Equal(4, command.Concurrency); Assert.Equal(512, command.ChunkSize);
        Assert.Equal(5368709120, command.MaxSizeCondition);
        Assert.Equal("no-cache", command.CacheControl);
        Assert.Equal(["a=b=c"], Assert.IsType<string[]>(command.MetadataInput));
        Assert.True(command.Overwrite); Assert.True(command.NoProgress);
    }


    [Fact]
    public void BatchBindsLegacyAliasesAndSafeDefaults()
    {
        var command = Assert.IsType<DownloadStorageBlobBatchCommand>(Bind("download-batch", new()
        {
            ["s"] = "content", ["d"] = "local", ["account-name"] = "fixture",
            ["pattern"] = "**/[!a]*.txt", ["dryrun"] = "true"
        }));
        Assert.Equal("content", command.Container);
        Assert.Equal("local", command.Destination);
        Assert.False(command.Overwrite);
        Assert.True(command.DryRun);
    }

    [Theory]
    [InlineData("upload", "file")]
    [InlineData("upload", "container-name")]
    [InlineData("download-batch", "source")]
    [InlineData("download-batch", "destination")]
    public void RequiredTransferInputsAreEnforced(string verb, string missing)
    {
        var arguments = verb == "download-batch"
            ? new Dictionary<string, object> { ["source"] = "content", ["destination"] = "local" }
            : new Dictionary<string, object> { ["container-name"] = "content", ["name"] = "data", ["file"] = "local" };
        arguments.Remove(missing);
        var error = Assert.Throws<UsageErrorException>(() => Bind(verb, arguments));
        Assert.Contains("Invalid parameter set:", error.Message);
        Assert.Contains(missing, error.Message);
    }

    [Theory]
    [InlineData("upload", "snapshot")]
    [InlineData("upload", "data")]
    [InlineData("download-batch", "version-id")]
    public void DeferredModesDoNotBind(string verb, string flag) =>
        Assert.Throws<UsageErrorException>(() => Bind(verb, new()
        {
            ["container-name"] = "content", ["name"] = "data", ["file"] = "local", [flag] = "unsupported"
        }));

    [Fact]
    public async Task BinaryConsoleSinkWritesAllBytesWithoutJsonTextOrNewline()
    {
        using var stream = new MemoryStream();
        byte[] bytes = [0, 10, 13, 255, 0xc3, 0xa9, 0, 34, 92];
        Assert.True(await CliBinaryOutput.TryWriteAsync(new BinaryOutputChunk(bytes), stream, null, default));
        Assert.True(await CliBinaryOutput.TryWriteAsync(new BinaryOutputChunk(new byte[] { 1 }), stream, null, default));
        Assert.Equal(bytes.Concat(new byte[] { 1 }), stream.ToArray());
        Assert.False(await CliBinaryOutput.TryWriteAsync(new { value = 1 }, stream, null, default));
    }

    [Theory]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("-9223372036854775808", long.MinValue)]
    [InlineData("5368709120", 5368709120L)]
    public void IntegralParserPreservesFullInt64Range(string text, long expected) =>
        Assert.Equal(expected, new PropertyParser(new ScopedLoggingService()).ConvertToType("range", text, typeof(long)));

    [Theory]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    [InlineData("5.5")]
    [InlineData("not-a-number")]
    public void InvalidInt64InputUsesTheNormalParserError(string text) =>
        Assert.Throws<PropertyParserException>(() => new PropertyParser(new ScopedLoggingService()).ConvertToType("range", text, typeof(long)));

    [Fact]
    public async Task BinaryQueryAndCancellationFailBeforeWriting()
    {
        using var stream = new MemoryStream();
        var chunk = new BinaryOutputChunk(new byte[] { 0, 255 });
        await Assert.ThrowsAsync<ArgumentException>(() => CliBinaryOutput.TryWriteAsync(chunk, stream, "data", default));
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CliBinaryOutput.TryWriteAsync(chunk, stream, null, stop.Token));
        Assert.Equal(0, stream.Length);
    }

    private static IProcessor Bind(string verb, Dictionary<string, object> arguments)
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("storage blob " + verb, ClientType.CLI);
        var logging = new ScopedLoggingService();
        var binder = new ArgumentParsingService(new CLIArgumentParsingProvider(new PropertyParser(logging), logging), new ProfileService(logging, new FilePersistenceStore()));
        var processor = Assert.IsAssignableFrom<IProcessor>(Activator.CreateInstance(metadata.ProcessorType));
        binder.BindArgumentsToProcessor(processor, arguments, metadata,
            new Profile { Name = "selected", TenantId = "tenant" });
        return processor;
    }
}
