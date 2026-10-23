// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public sealed record BinaryOutputChunk(ReadOnlyMemory<byte> Bytes);
