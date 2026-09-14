// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Output;
using Core.Cli.Parser;
using Core.Configuration.Options;
using Core.Runtime;

namespace Core.Cli;

/// <summary>
/// Log level, color mode, and output format resolved from config + this invocation's global flags.
/// <see cref="Format"/> is the default for object/array results; <see cref="SimpleFormat"/> is the
/// default for scalar results (see <see cref="OutputFormatPolicy.ResolveEffectiveSimpleFormat(ClientsOptions, OutputFormat?)"/>).
/// </summary>
internal readonly record struct EffectiveSessionSettings(CliLogLevel LogLevel, ColorMode Color, OutputFormat Format, OutputFormat SimpleFormat);

/// <summary>
/// Aggregates the per-session policy resolutions (<see cref="CliLoggingPolicy"/>,
/// <see cref="ColorPolicy"/>, <see cref="OutputFormatPolicy"/>) into a single call site instead of
/// three separate ones in <c>Program.Main</c>. Each underlying policy stays its own concern; this
/// is composition only, not merged logic.
/// </summary>
internal static class SessionPolicyResolver
{
    public static EffectiveSessionSettings Resolve(CoreOptions coreOptions, ClientsOptions clientsOptions, GlobalArgs globalArgs) => new(
        CliLoggingPolicy.ResolveEffectiveLevel(coreOptions, globalArgs.Debug),
        ColorPolicy.Parse(coreOptions.Output.Color),
        OutputFormatPolicy.ResolveEffectiveFormat(clientsOptions, globalArgs.Output),
        OutputFormatPolicy.ResolveEffectiveSimpleFormat(clientsOptions, globalArgs.Output));
}
