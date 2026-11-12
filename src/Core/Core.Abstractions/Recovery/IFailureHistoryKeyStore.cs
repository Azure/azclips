// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Recovery;

public interface IFailureHistoryKeyStore
{
    /// <summary>
    /// Returns an owned copy of the protected key.
    /// Missing keys must fail when creation is disabled. Native operations must never prompt.
    /// The caller must clear the returned bytes after use, serialize creation across processes,
    /// and isolate uncancellable native latency when a request has a hard deadline.
    /// </summary>
    /// <param name="replaced">
    /// Called when creation is allowed and a stored key that can never become readable has been deleted to be replaced.
    /// </param>
    Task<byte[]> GetKeyAsync(bool createIfMissing, Action? replaced = null, CancellationToken cancellationToken = default);
}
