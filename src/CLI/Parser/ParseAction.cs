// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Extensions;

namespace Core.Cli.Parser;

/// <summary>
/// Consumes tokens for a matched global flag and mutates a <see cref="GlobalArgsBuilder"/>
/// accordingly. Each <see cref="KnownGlobalFlag"/> pairs its docs metadata with a
/// <see cref="ParseAction"/> so <see cref="GlobalArgParser"/> dispatches through
/// <see cref="KnownGlobalFlags.All"/> instead of a per-flag switch. Mirrors argparse's
/// <c>Action</c> shape from azure-cli's globals model.
/// </summary>
public abstract class ParseAction
{
    /// <summary>
    /// Called when <paramref name="args"/>[<paramref name="index"/>] matched this flag's name
    /// or one of its aliases. Returns the number of tokens consumed (typically 1 for boolean
    /// toggles, 2 for value-taking flags).
    /// </summary>
    public abstract int Apply(GlobalArgsBuilder builder, ReadOnlySpan<string> args, int index);
}

/// <summary>Mutable staging area for parsed global flags. <see cref="GlobalArgParser"/> converts
/// this into an immutable <see cref="GlobalArgs"/> once parsing completes.</summary>
public sealed class GlobalArgsBuilder
{
    public bool Debug { get; set; }
    public OutputFormat Output { get; set; } = OutputFormat.Plain;
    public bool NoCache { get; set; }
}

/// <summary>Sets a bool field on the builder unconditionally. Used for <c>--debug</c>, <c>--no-cache</c>.</summary>
internal sealed class BoolToggleAction : ParseAction
{
    private readonly Action<GlobalArgsBuilder> _setter;

    public BoolToggleAction(Action<GlobalArgsBuilder> setter) => _setter = setter;

    public override int Apply(GlobalArgsBuilder builder, ReadOnlySpan<string> args, int index)
    {
        _setter(builder);
        return 1;
    }
}

/// <summary>
/// Handles <c>--output</c>: accepts both <c>--output json</c> (two tokens) and <c>--output=json</c>
/// (single token). Unknown values raise a usage error.
/// </summary>
internal sealed class OutputFormatAction : ParseAction
{
    public override int Apply(GlobalArgsBuilder builder, ReadOnlySpan<string> args, int index)
    {
        var token = args[index];
        var eq = token.IndexOf('=');
        if (eq >= 0)
        {
            builder.Output = ParseValue(token[(eq + 1)..]);
            return 1;
        }

        if (index + 1 >= args.Length)
        {
            throw AzclipsErrorException.UsageError(
                "--output requires a value ('json', 'jsonc', 'yaml', 'yamlc', or 'plain').",
                new Dictionary<string, object?> { ["flag"] = "--output" });
        }

        builder.Output = ParseValue(args[index + 1]);
        return 2;
    }

    private static OutputFormat ParseValue(string value)
    {
        if (value.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return OutputFormat.Json;
        }

        if (value.Equals("jsonc", StringComparison.OrdinalIgnoreCase))
        {
            return OutputFormat.Jsonc;
        }

        if (value.Equals("yaml", StringComparison.OrdinalIgnoreCase))
        {
            return OutputFormat.Yaml;
        }

        if (value.Equals("yamlc", StringComparison.OrdinalIgnoreCase))
        {
            return OutputFormat.Yamlc;
        }

        if (value.Equals("plain", StringComparison.OrdinalIgnoreCase))
        {
            return OutputFormat.Plain;
        }

        throw AzclipsErrorException.UsageError(
            $"--output value '{value}' is not recognized. Expected 'json', 'jsonc', 'yaml', 'yamlc', or 'plain'.",
            new Dictionary<string, object?>
            {
                ["flag"] = "--output",
                ["value"] = value,
            });
    }
}
