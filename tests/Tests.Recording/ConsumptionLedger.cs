// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Tests.Recording;

/// <summary>
/// Persists which recording entries playback has consumed, one index per line, so consumption
/// survives across the processes of a scripted scenario. Commands in a script run sequentially,
/// so appends need no cross-process locking.
/// </summary>
public sealed class ConsumptionLedger(string path)
{
    public IReadOnlyCollection<int> Read()
        => !File.Exists(path)
            ? []
            : File.ReadAllLines(path).Where(l => l.Length > 0).Select(int.Parse).ToArray();

    public void Append(int index)
        => File.AppendAllText(path, index + Environment.NewLine);
}
