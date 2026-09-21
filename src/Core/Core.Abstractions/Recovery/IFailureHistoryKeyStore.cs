// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Recovery;

public interface IFailureHistoryKeyStore
{
    /// <summary>
    /// Returns an owned copy of the protected key. Missing keys must fail when creation is disabled.
    /// The caller must clear the returned bytes after use and serialize key creation across processes.
    /// </summary>
    Task<byte[]> GetKeyAsync(bool createIfMissing, CancellationToken cancellationToken = default);
}
