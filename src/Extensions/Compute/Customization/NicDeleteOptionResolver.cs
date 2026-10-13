// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

internal static class NicDeleteOptionResolver
{
    private static readonly string[] s_allowedValues = ["Delete", "Detach"];

    internal static string? ResolveForNewNic(IReadOnlyList<string> cliValues, string? powerShellValue)
    {
        EnsureSingleSurface(cliValues, powerShellValue);
        if (powerShellValue is not null)
        {
            return Normalize(powerShellValue);
        }

        if (cliValues.Count == 0)
        {
            return null;
        }

        if (cliValues.Count != 1 || cliValues[0].Contains('='))
        {
            throw AzclipsErrorException.UsageError(
                "When creating a network interface, --nic-delete-option must be a single value: Delete or Detach.");
        }

        return Normalize(cliValues[0]);
    }

    internal static List<string?> ResolveForExistingNics(
        IReadOnlyList<string> nicInputs,
        IReadOnlyList<string> cliValues,
        string? powerShellValue)
    {
        EnsureSingleSurface(cliValues, powerShellValue);
        if (powerShellValue is not null)
        {
            string normalized = Normalize(powerShellValue);
            return nicInputs.Select(_ => (string?)normalized).ToList();
        }

        if (cliValues.Count == 0)
        {
            return nicInputs.Select(_ => (string?)null).ToList();
        }

        bool usesAssignments = cliValues.Any(value => value.Contains('='));
        if (!usesAssignments)
        {
            if (cliValues.Count != 1)
            {
                throw AzclipsErrorException.UsageError(
                    "--nic-delete-option accepts one value for all NICs, or NAME=VALUE assignments for individual NICs.");
            }

            string normalized = Normalize(cliValues[0]);
            return nicInputs.Select(_ => (string?)normalized).ToList();
        }

        if (cliValues.Any(value => !value.Contains('=')))
        {
            throw AzclipsErrorException.UsageError(
                "Do not mix a shared --nic-delete-option value with NAME=VALUE assignments.");
        }

        var assignments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string value in cliValues)
        {
            string[] parts = value.Split('=', 2);
            if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                throw AzclipsErrorException.UsageError(
                    $"Invalid NIC delete option '{value}'. Use NAME=Delete or NAME=Detach.");
            }

            if (!assignments.TryAdd(parts[0], Normalize(parts[1])))
            {
                throw AzclipsErrorException.UsageError(
                    $"NIC delete option for '{parts[0]}' was specified more than once.");
            }
        }

        var knownNics = new HashSet<string>(nicInputs, StringComparer.OrdinalIgnoreCase);
        string? unknownNic = assignments.Keys.FirstOrDefault(name => !knownNics.Contains(name));
        if (unknownNic is not null)
        {
            throw AzclipsErrorException.UsageError(
                $"NIC delete option target '{unknownNic}' was not supplied through --nics.");
        }

        return nicInputs.Select(input => assignments.GetValueOrDefault(input)).ToList();
    }

    private static void EnsureSingleSurface(IReadOnlyList<string> cliValues, string? powerShellValue)
    {
        if (cliValues.Count > 0 && powerShellValue is not null)
        {
            throw AzclipsErrorException.UsageError(
                "Specify only one network interface delete option parameter.");
        }
    }

    private static string Normalize(string value)
    {
        string? normalized = s_allowedValues.FirstOrDefault(
            allowed => allowed.Equals(value, StringComparison.OrdinalIgnoreCase));
        return normalized
            ?? throw AzclipsErrorException.UsageError(
                $"Network interface delete option must be one of: {string.Join(", ", s_allowedValues)}.");
    }
}
