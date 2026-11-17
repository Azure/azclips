// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli;

namespace Cli.Tests.Recovery;

public class AutomaticRecoveryExitCodeTests
{
    [Theory]
    [InlineData(false, 2, 0, 2)]
    [InlineData(false, 2, 1, 2)]
    [InlineData(false, 1, 0, 1)]
    [InlineData(true, 2, 0, 0)]
    [InlineData(true, 2, 5, 5)]
    [InlineData(false, 2, 130, 130)]
    [InlineData(true, 2, 130, 130)]
    public void Automatic_OneshotKeepsTheOriginalCodeAndInteractiveReturnsTheAgentCode(
        bool interactive, int original, int agentExit, int expected) =>
        Assert.Equal(expected, Program.AutomaticRecoveryExitCode(interactive, original, agentExit));
}
