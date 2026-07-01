// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Cirrus.Extensions.Version;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// The Version extension's <see cref="ExtensionIndex"/> is a CLI-only built-in (no PowerShell
/// module). It must expose exactly one command — the bare <c>version</c> verb — under the CLI
/// client, and an empty table under PowerShell (mirroring the <c>extension</c> built-in). These
/// are pure lookup assertions with no DI or filesystem dependencies.
/// </summary>
public class VersionExtensionIndexTests
{
    private static readonly ExtensionIndex Index = new();

    /// <summary>
    /// The extension contributes exactly one top-level verb, <c>version</c>. This is the value the
    /// dispatcher and built-in seed key off, so it must not drift.
    /// </summary>
    [Fact]
    public void GetVerbs_ShouldReturnOnlyVersion()
    {
        Assert.Equal(new[] { "version" }, Index.GetVerbs().ToArray());
    }

    /// <summary>
    /// Under the CLI client, the bare <c>version</c> key resolves to the GetVersionProcessor
    /// metadata. Verifies the invocation name, processor name, and owning assembly the loader uses
    /// to locate and run the command.
    /// </summary>
    [Fact]
    public void GetCommandMetadata_Cli_ShouldResolveVersionVerb()
    {
        var metadata = Index.GetCommandMetadata("version", ClientType.CLI);

        Assert.Equal("version", metadata.InvocationName);
        Assert.Equal("GetVersionProcessor", metadata.Name);
        Assert.Equal("Microsoft.Azure.Cirrus.Extensions.Version", metadata.AssemblyName);
    }

    /// <summary>
    /// The CLI command table exposes exactly one key (<c>version</c>) — the extension has no
    /// sub-commands. Guards against accidentally adding or renaming CLI commands.
    /// </summary>
    [Fact]
    public void GetCommandKeys_Cli_ShouldContainOnlyVersion()
    {
        Assert.Equal(new[] { "version" }, Index.GetCommandKeys(ClientType.CLI).ToArray());
    }

    /// <summary>
    /// Version is CLI-only, so the PowerShell command table is empty. This encodes the decision to
    /// ship no <c>Get-CirrusVersion</c> cmdlet (mirroring the <c>extension</c> built-in).
    /// </summary>
    [Fact]
    public void GetCommandKeys_PowerShell_ShouldBeEmpty()
    {
        Assert.Empty(Index.GetCommandKeys(ClientType.PowerShell));
    }

    /// <summary>
    /// The PowerShell command enumeration is also empty (same CLI-only rationale as the keys test),
    /// confirming both lookup paths agree that no cmdlet is exposed.
    /// </summary>
    [Fact]
    public void GetAllCommands_PowerShell_ShouldBeEmpty()
    {
        // Version ships no cmdlet, so the PS table is intentionally empty.
        Assert.Empty(Index.GetAllCommands(ClientType.PowerShell));
    }

    /// <summary>
    /// Resolving a name that isn't in the table throws <see cref="KeyNotFoundException"/> rather than
    /// returning null, so the dispatcher fails fast on an unknown command.
    /// </summary>
    [Fact]
    public void GetCommandMetadata_UnknownName_ShouldThrowKeyNotFound()
    {
        Assert.Throws<KeyNotFoundException>(() => Index.GetCommandMetadata("nope", ClientType.CLI));
    }
}
