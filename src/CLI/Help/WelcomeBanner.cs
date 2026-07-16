// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Help;

/// <summary>
/// Small ASCII banner printed above the root help output when the user runs
/// bare <c>az</c> (no args). Intentionally NOT printed for <c>az --help</c> —
/// the banner marks the "you just landed here" case, not every help view.
/// </summary>
internal static class WelcomeBanner
{
    // Figlet "small" font for "AzCLIPS". Do NOT trim trailing whitespace — glyphs rely on it.
    private static readonly string[] s_bannerLines =
    [
        @"     _        ___ _    ___ ___  ___ ",
        @"    /_\   ___/ __| |  |_ _| _ \/ __|",
        @"   / _ \ |_ / (__| |__ | ||  _/\__ \",
        @"  /_/ \_\/__|\___|____|___|_|  |___/",
    ];

    public static void Write(TextWriter output)
    {
        output.WriteLine();
        foreach (var line in s_bannerLines)
        {
            output.WriteLine(line);
        }
        output.WriteLine();
        output.WriteLine("  Welcome to AzCLIPS - Azure CLI, PowerShell & automation, in one host.");
        output.WriteLine("  Run `az --help` for command groups, or `az <verb> --help` for a specific verb.");
        output.WriteLine();
    }
}
