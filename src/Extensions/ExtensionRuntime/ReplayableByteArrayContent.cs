// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

/// <summary>
/// Binary content whose retry copies share the payload without sharing disposable streams.
/// The caller must not modify the buffer until the request and all retries finish.
/// </summary>
public sealed class ReplayableByteArrayContent : ByteArrayContent
{
    private readonly byte[] buffer;

    public ReplayableByteArrayContent(byte[] buffer) : base(buffer) => this.buffer = buffer;

    internal ReplayableByteArrayContent CreateReplay() => new(buffer);
}
