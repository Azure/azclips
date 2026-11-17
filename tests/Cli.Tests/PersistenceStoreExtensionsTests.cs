// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Core.Abstractions;
using Core.Runtime;
using Core.Runtime.Persistence;
using Moq;

namespace Cli.Tests;

public sealed class PersistenceStoreExtensionsTests
{
    [Fact]
    public void TextUtilitiesLiveInRuntimeAssembly()
    {
        Assert.Same(typeof(ProfileService).Assembly, typeof(PersistenceStoreExtensions).Assembly);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("\u65e5\u672c\u8a9e \ud83d\ude00")]
    public async Task WriteTextAsyncDelegatesUtf8BytesAndCancellation(string text)
    {
        var store = new Mock<IPersistenceStore>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        var expected = Encoding.UTF8.GetBytes(text);
        store.Setup(s => s.WriteAsync("state", It.Is<ReadOnlyMemory<byte>>(bytes =>
                bytes.ToArray().SequenceEqual(expected)), cancellation.Token))
            .Returns(Task.CompletedTask);

        await store.Object.WriteTextAsync("state", text, cancellation.Token);

        store.VerifyAll();
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("\u65e5\u672c\u8a9e \ud83d\ude00")]
    public async Task ReadTextAsyncDecodesUtf8AndForwardsCancellation(string text)
    {
        var store = new Mock<IPersistenceStore>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        store.Setup(s => s.ReadAsync("state", cancellation.Token))
            .ReturnsAsync(Encoding.UTF8.GetBytes(text));

        Assert.Equal(text, await store.Object.ReadTextAsync("state", cancellation.Token));
        store.VerifyAll();
    }

    [Theory]
    [InlineData(65001, "")]
    [InlineData(65001, "\u65e5\u672c\u8a9e \ud83d\ude00")]
    [InlineData(1200, "")]
    [InlineData(1200, "\u65e5\u672c\u8a9e \ud83d\ude00")]
    [InlineData(1201, "")]
    [InlineData(1201, "\u65e5\u672c\u8a9e \ud83d\ude00")]
    [InlineData(12000, "")]
    [InlineData(12000, "\u65e5\u672c\u8a9e \ud83d\ude00")]
    [InlineData(12001, "")]
    [InlineData(12001, "\u65e5\u672c\u8a9e \ud83d\ude00")]
    public async Task ReadTextAsyncDetectsBomEncoding(int codePage, string text)
    {
        var encoding = Encoding.GetEncoding(codePage);
        var content = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        var store = new Mock<IPersistenceStore>(MockBehavior.Strict);
        store.Setup(s => s.ReadAsync("state", default)).ReturnsAsync(content);

        Assert.Equal(text, await store.Object.ReadTextAsync("state"));
    }

    [Fact]
    public async Task ReadTextAsyncPreservesMissingFile()
    {
        var store = new Mock<IPersistenceStore>(MockBehavior.Strict);
        store.Setup(s => s.ReadAsync("missing", default)).ReturnsAsync((byte[]?)null);

        Assert.Null(await store.Object.ReadTextAsync("missing"));
    }

    [Fact]
    public async Task ReadTextAsyncPropagatesStorageFailure()
    {
        var failure = new IOException("Read failed");
        var store = new Mock<IPersistenceStore>(MockBehavior.Strict);
        store.Setup(s => s.ReadAsync("state", default)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => store.Object.ReadTextAsync("state")));
    }

    [Fact]
    public async Task WriteTextAsyncPropagatesStorageFailure()
    {
        var failure = new IOException("Write failed");
        var store = new Mock<IPersistenceStore>(MockBehavior.Strict);
        store.Setup(s => s.WriteAsync("state", It.IsAny<ReadOnlyMemory<byte>>(), default)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => store.Object.WriteTextAsync("state", "text")));
    }

    [Fact]
    public async Task CancelledTextOperationsDoNotAccessStorage()
    {
        var store = new Mock<IPersistenceStore>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.Object.ReadTextAsync("state", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.Object.WriteTextAsync("state", "text", cancellation.Token));
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadTextAsyncRejectsInvalidUtf8()
    {
        var store = new Mock<IPersistenceStore>(MockBehavior.Strict);
        store.Setup(s => s.ReadAsync("state", default)).ReturnsAsync(new byte[] { 0xff });

        await Assert.ThrowsAsync<DecoderFallbackException>(() => store.Object.ReadTextAsync("state"));
    }

    [Fact]
    public async Task WriteTextAsyncRejectsInvalidUnicode()
    {
        var store = new Mock<IPersistenceStore>(MockBehavior.Strict);

        await Assert.ThrowsAsync<EncoderFallbackException>(() => store.Object.WriteTextAsync("state", "\ud800"));
        store.VerifyNoOtherCalls();
    }
}
