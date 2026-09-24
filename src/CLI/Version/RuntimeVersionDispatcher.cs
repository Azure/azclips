// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Core.Abstractions;
using Core.Cli.Output;
using Core.Cli.Parser;
using Core.Extensions;

namespace Core.Cli.Version;

/// <summary>
/// Output shape for a version request. <see cref="Json"/> is the structured form used by the
/// <c>az version</c> command (parseable, script-friendly); <see cref="Text"/> is the human-readable
/// form used by the <c>az --version</c> global flag.
/// </summary>
internal enum VersionOutputFormat
{
    Json,
    Text
}

/// <summary>
/// Renders CLI version information to the console on the host — the analogue of
/// <see cref="Help.RuntimeHelpDispatcher"/> for <c>az version</c> / <c>az --version</c>. Both
/// syntaxes share <see cref="VersionInformationProvider"/> for data and differ only in rendering:
/// </summary>
internal sealed class RuntimeVersionDispatcher
{
    private readonly IConfigurationService _configurationService;
    private readonly TextWriter _output;

    public RuntimeVersionDispatcher(IConfigurationService configurationService, TextWriter output)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public int Render(VersionOutputFormat format, string? query = null)
    {
        var info = VersionInformationProvider.Gather(_configurationService);

        if (format == VersionOutputFormat.Text)
        {
            if (query is not null)
            {
                throw AzclipsErrorException.UsageError(
                    "--query is not supported with `az --version`; use `az version --query ...` instead.",
                    new Dictionary<string, object?> { ["flag"] = "--query", ["format"] = "text" });
            }

            var rendered = FormatText(info);
            _output.WriteLine(rendered);
            return 0;
        }

        var renderedJson = CliOutputFormatter.FormatValue(info, OutputFormat.Json, query: query);
        _output.WriteLine(renderedJson);
        return 0;
    }

    // Compact, human-readable projection of VersionInformation
    private static string FormatText(VersionInformation info)
    {
        const string Indent = "    ";
        // Widest indented label is "Architecture:" (13 chars); +2 gives a comfortable gap.
        // Change one constant if the label set grows.
        const int maxLabelWidth = 15;

        var builder = new StringBuilder();
        builder.AppendLine($"{"AzCli".PadRight(Indent.Length + maxLabelWidth)}{info.AzCli}");
        builder.AppendLine();
        builder.AppendLine("Runtime");
        builder.AppendLine($"{Indent}{".NET:".PadRight(maxLabelWidth)}{info.Runtime.DotNet}");
        builder.AppendLine($"{Indent}{"OS:".PadRight(maxLabelWidth)}{info.Runtime.Os}");
        builder.AppendLine($"{Indent}{"Architecture:".PadRight(maxLabelWidth)}{info.Runtime.Architecture}");
        builder.AppendLine();
        builder.AppendLine("Paths");
        builder.AppendLine($"{Indent}{"Install:".PadRight(maxLabelWidth)}{info.Paths.InstallLocation}");
        builder.AppendLine($"{Indent}{"Config:".PadRight(maxLabelWidth)}{info.Paths.ConfigLocation}");
        builder.AppendLine($"{Indent}{"Extensions:".PadRight(maxLabelWidth)}{info.Paths.ExtensionsLocation}");

        return builder.ToString().TrimEnd();
    }
}
