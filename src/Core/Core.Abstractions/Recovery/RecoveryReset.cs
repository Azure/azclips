// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Recovery;

/// <summary>Tracks recovery history resets for in-process clients.</summary>
public static class RecoveryReset
{
    private static long s_generation;

    public static long Generation => Volatile.Read(ref s_generation);

    public static void Advance() => Interlocked.Increment(ref s_generation);
}
