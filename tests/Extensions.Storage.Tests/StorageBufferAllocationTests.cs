// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;

namespace Extensions.Storage.Tests;

public class StorageBufferAllocationTests
{
    [Fact]
    public async Task FileChunksReuseCallerMemoryAndRespectItsBounds()
    {
        string path = Path.GetTempFileName();
        try
        {
            byte[] payload = Enumerable.Range(0, 32).Select(index => (byte)index).ToArray();
            await File.WriteAllBytesAsync(path, payload);
            await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 1, FileOptions.Asynchronous | FileOptions.RandomAccess);
            byte[] buffer = Enumerable.Repeat((byte)255, 12).ToArray();
            foreach (int offset in new[] { 3, 19 })
            {
                await StorageWireFormat.ReadFileChunkAsync(source, offset, buffer.AsMemory(2, 8), CancellationToken.None);
                Assert.Equal(payload[offset..(offset + 8)], buffer[2..10]);
                Assert.All(buffer[..2].Concat(buffer[10..]), value => Assert.Equal(255, value));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task BinaryRetryClonesSharePayloadAndHaveIndependentLifetimes()
    {
        byte[] bytes = new byte[StorageWireFormat.MaximumChunkBytes];
        Random.Shared.NextBytes(bytes);
        using var original = new HttpRequestMessage(HttpMethod.Put, "https://fixture.invalid/block")
        {
            Content = StorageWireFormat.CreateContent(bytes)
        };
        original.Content!.Headers.Add("x-fixture", "preserved");
        long before = GC.GetAllocatedBytesForCurrentThread();
        using var clone = await original.CloneWithContent();
        using var retry = await clone.CloneWithContent();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 0, 64 * 1024);
        Assert.IsType<ReplayableByteArrayContent>(clone.Content);
        original.Dispose();
        clone.Dispose();
        Assert.Equal(bytes, await retry.Content!.ReadAsByteArrayAsync());
        Assert.Equal("preserved", Assert.Single(retry.Content.Headers.GetValues("x-fixture")));
        Assert.Equal("application/octet-stream", retry.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task KnownLengthChunkAllocatesOnlyItsReturnedBuffer()
    {
        byte[] bytes = new byte[StorageWireFormat.MaximumChunkBytes];
        Random.Shared.NextBytes(bytes);
        using var content = new ByteArrayContent(bytes);
        long before = GC.GetAllocatedBytesForCurrentThread();
        byte[] result = await StorageWireFormat.ReadChunkAsync(content, CancellationToken.None);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(bytes, result);
        Assert.InRange(allocated, bytes.Length, bytes.Length + 64 * 1024);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public async Task KnownLengthChunkRejectsIncorrectDeclaredLength(int declared, int actual)
    {
        using var content = new ByteArrayContent(new byte[actual]);
        content.Headers.ContentLength = declared;
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            StorageWireFormat.ReadChunkAsync(content, CancellationToken.None));
    }
}
