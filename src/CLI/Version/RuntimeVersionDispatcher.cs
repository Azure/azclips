// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Core.Abstractions;

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
/// Renders CLI version information to the console on the host cold-path — the analogue of
/// <see cref="Help.RuntimeHelpDispatcher"/> for <c>az version</c> / <c>az --version</c>. Both
/// syntaxes share <see cref="VersionInformationProvider"/> for data and differ only in rendering:
/// JSON reuses <see cref="CliOutputFormatter"/> (identical to how the old command's output was
/// serialized), while text is a compact, human-readable layout.
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

    public int Render(VersionOutputFormat format)
    {
        var info = VersionInformationProvider.Gather(_configurationService);

        var rendered = format == VersionOutputFormat.Text
            ? FormatText(info)
            : CliOutputFormatter.FormatValue(info);

        _output.WriteLine(rendered);
        return 0;
    }

    // Compact, human-readable projection of the same data the JSON form carries. Mirrors the
    // familiar `az --version` style: the CLI version first, then runtime and path sections.
    private static string FormatText(VersionInformation info)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"AzCli             {info.AzCli}");
        builder.AppendLine();
        builder.AppendLine("Runtime");
        builder.AppendLine($"    .NET:         {info.Runtime.DotNet}");
        builder.AppendLine($"    OS:           {info.Runtime.Os}");
        builder.AppendLine($"    Architecture: {info.Runtime.Architecture}");
        builder.AppendLine();
        builder.AppendLine("Paths");
        builder.AppendLine($"    Install:      {info.Paths.InstallLocation}");
        builder.AppendLine($"    Config:       {info.Paths.ConfigLocation}");

        return builder.ToString();
    }
}
