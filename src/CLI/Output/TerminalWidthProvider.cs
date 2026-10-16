// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli.Output;

internal static class TerminalWidthProvider
{
    internal static int? Get()
    {
        if (Console.IsOutputRedirected)
            return null;

        try
        {
            var width = Console.WindowWidth;
            return width > 0 ? width : null;
        }
        catch
        {
            return null;
        }
    }
}
