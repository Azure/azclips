// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Tests.Common.Utilities;

namespace Cli.Tests.Telemetry;

public class ProgramTelemetryActivityNameTests
{
    [Fact]
    public void DispatchHelp_UsesHelpActivityName()
    {
        var programPath = Path.Combine(RepoLocator.GetRepoRoot(), "src", "CLI", "Program.cs");
        var source = File.ReadAllText(programPath);

        var methodStart = source.IndexOf("private static int DispatchHelp", StringComparison.Ordinal);
        var nextMethod = source.IndexOf("private static void EmitStructuredError", methodStart, StringComparison.Ordinal);
        var methodBody = source[methodStart..nextMethod];

        Assert.Contains("_telemetryService?.StartActivity(\"AzureCLI.DispatchHelp\")", methodBody);
    }
}
