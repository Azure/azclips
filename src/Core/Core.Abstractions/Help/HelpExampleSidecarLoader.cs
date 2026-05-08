// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Core.Abstractions;

public static partial class HelpExampleSidecarLoader
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static HelpExampleSet Load(string baseDirectory, string processorName, IEnumerable<string>? seedExamples = null)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException("Base directory cannot be null or empty.", nameof(baseDirectory));
        }

        if (string.IsNullOrWhiteSpace(processorName))
        {
            throw new ArgumentException("Processor name cannot be null or empty.", nameof(processorName));
        }

        var cliExamples = ReadSidecarExamples(baseDirectory, processorName, "cli");
        var psExamples = ReadSidecarExamples(baseDirectory, processorName, "ps");

        if (seedExamples != null)
        {
            // Sidecars are authoritative; seed examples only fill missing surface entries.
            var parsedSeeds = ParseSeedExamples(seedExamples);

            if (cliExamples.Count == 0)
            {
                cliExamples.AddRange(parsedSeeds.Cli);
            }

            if (psExamples.Count == 0)
            {
                psExamples.AddRange(parsedSeeds.PowerShell);
            }
        }

        return new HelpExampleSet(cliExamples, psExamples);
    }

    private static List<HelpExample> ReadSidecarExamples(string baseDirectory, string processorName, string surface)
    {
        var filePath = Path.Combine(baseDirectory, $"{processorName}.examples.{surface}.json");
        if (!File.Exists(filePath))
        {
            return [];
        }

        var content = File.ReadAllText(filePath);
        SidecarPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SidecarPayload>(content, s_jsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse help examples sidecar: {filePath}", ex);
        }

        if (payload?.Examples == null)
        {
            return [];
        }

        var examples = new List<HelpExample>();
        foreach (var item in payload.Examples)
        {
            if (item == null)
            {
                continue;
            }

            var title = (item.Title ?? string.Empty).Trim();
            var command = (item.Command ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            examples.Add(new HelpExample(title, command));
        }

        return examples;
    }

    private static HelpExampleSet ParseSeedExamples(IEnumerable<string> seedExamples)
    {
        var cli = new List<HelpExample>();
        var ps = new List<HelpExample>();

        foreach (var seed in seedExamples)
        {
            if (string.IsNullOrWhiteSpace(seed))
            {
                continue;
            }

            var separatorIndex = seed.IndexOf('|');
            if (separatorIndex < 0)
            {
                continue;
            }

            var title = seed[..separatorIndex].Trim();
            var command = seed[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            var example = new HelpExample(title, command);
            if (LooksLikePowerShellCommand(command))
            {
                ps.Add(example);
            }
            else
            {
                cli.Add(example);
            }
        }

        return new HelpExampleSet(cli, ps);
    }

    private static bool LooksLikePowerShellCommand(string command)
    {
        // Matches common Verb-Noun shape: New-ResourceGroup, Connect-AzAccount, etc.
        return PsVerbNounRegex().IsMatch(command);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*-[A-Za-z][A-Za-z0-9-]*")]
    private static partial Regex PsVerbNounRegex();

    private sealed record SidecarPayload(string? CommandId, List<SidecarExample>? Examples);

    private sealed record SidecarExample(string? Title, string? Command);
}
