// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Extensions;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Drift guard: <see cref="BuiltInExtensions.Map"/> hand-maintains a <c>Leaves</c> array per
/// built-in that must mirror that module's <c>s_cliTable</c> keys (minus the verb prefix).
/// Otherwise dispatch's longest-prefix match silently breaks — e.g. <c>az extension search
/// foo</c> falls into the verb-only fallback and joins <c>"search foo"</c> as one command.
/// Asserting the two sources agree turns that runtime regression into a CI failure.
/// </summary>
public class BuiltInExtensionsLeavesDriftTests
{
    public static TheoryData<string, IExtensionIndex> BuiltIns =>
        new()
        {
            { "extension", new Microsoft.Azure.Cirrus.Extensions.Extension.ExtensionIndex() },
            { "login",     new Microsoft.Azure.Azclips.Extensions.Login.ExtensionIndex()     },
            { "config",    new Microsoft.Azure.Cirrus.Extensions.Config.ExtensionIndex()    },
        };

    [Theory]
    [MemberData(nameof(BuiltIns))]
    public void BuiltInLeaves_ShouldMatch_SCliTableKeys(string verb, IExtensionIndex index)
    {
        var seedLeaves = BuiltInExtensions.Map[verb].Leaves
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToArray();

        // s_cliTable keys are full paths ("extension add", "login show", or just "login" for
        // the bare-verb leaf). Strip the "{verb} " prefix; the bare-verb leaf becomes empty
        // string and is seeded implicitly by LeafIndex, so it's not expected in Leaves.
        var prefix = verb + " ";
        var derivedLeaves = index.GetCommandKeys(ClientType.CLI)
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k => k[prefix.Length..])
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(derivedLeaves, seedLeaves);
    }
}
