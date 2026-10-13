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

        string?[] resolvedOptions = new string?[nicInputs.Count];
        bool[] assigned = new bool[nicInputs.Count];

        foreach (string value in cliValues)
        {
            string[] parts = value.Split('=', 2);
            if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                throw AzclipsErrorException.UsageError(
                    $"Invalid NIC delete option '{value}'. Use NAME=Delete or NAME=Detach.");
            }

            string targetKey = parts[0].Trim();
            string normalizedValue = Normalize(parts[1]);

            List<int> matchingIndices = [];
            for (int i = 0; i < nicInputs.Count; i++)
            {
                if (string.Equals(nicInputs[i], targetKey, StringComparison.OrdinalIgnoreCase))
                {
                    matchingIndices.Add(i);
                }
            }

            if (matchingIndices.Count == 0)
            {
                for (int i = 0; i < nicInputs.Count; i++)
                {
                    if (string.Equals(GetNicName(nicInputs[i]), targetKey, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingIndices.Add(i);
                    }
                }
            }

            if (matchingIndices.Count == 0)
            {
                throw AzclipsErrorException.UsageError(
                    $"NIC delete option target '{targetKey}' was not supplied through --nics.");
            }

            if (matchingIndices.Count > 1)
            {
                throw AzclipsErrorException.UsageError(
                    $"NIC delete option target '{targetKey}' is ambiguous because multiple NICs share that name. Specify the full resource ID instead.");
            }

            int targetIndex = matchingIndices[0];
            if (assigned[targetIndex])
            {
                throw AzclipsErrorException.UsageError(
                    $"NIC delete option for '{targetKey}' was specified more than once.");
            }

            assigned[targetIndex] = true;
            resolvedOptions[targetIndex] = normalizedValue;
        }

        return resolvedOptions.ToList();
    }

    private static string GetNicName(string input)
    {
        string trimmed = input.Trim().TrimEnd('/');
        int lastSlash = trimmed.LastIndexOf('/');
        return lastSlash >= 0 ? trimmed[(lastSlash + 1)..] : trimmed;
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
