// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

/// <summary>
/// Binary content whose retry copies share the payload without sharing disposable streams.
/// The caller must not modify the buffer until the request and all retries finish.
/// </summary>
public sealed class ReplayableByteArrayContent : ByteArrayContent, IReplayableHttpContent
{
    private readonly byte[] buffer;

    public ReplayableByteArrayContent(byte[] buffer) : base(buffer) => this.buffer = buffer;

    public HttpContent CreateReplay() => new ReplayableByteArrayContent(buffer);
}

/// <summary>Request content that creates an independent copy of itself for each retry attempt.</summary>
public interface IReplayableHttpContent
{
    HttpContent CreateReplay();
}
