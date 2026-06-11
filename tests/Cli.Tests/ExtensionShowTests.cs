using System.Collections;
using Core.Extensions;
using Microsoft.Azure.Cirrus.Extensions.Extension.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Verifies <see cref="ShowExtensionProcessor.BuildResult"/> exposes the v2
/// <see cref="ExtensionRegistryEntry.Namespaces"/> field. Without this, callers of
/// <c>extension show</c> on a multi-namespace extension only saw the legacy single <c>Verb</c>.
/// (PR #253 review feedback.)
/// </summary>
public class ExtensionShowTests
{
    private const string PackageId = "Microsoft.Azure.Cirrus.Extensions.Resources";

    private static IReadOnlyList<string> AsList(object? value)
    {
        Assert.NotNull(value);
        var list = new List<string>();
        foreach (var item in (IEnumerable)value!)
        {
            list.Add((string)item!);
        }
        return list;
    }

    [Fact]
    public void BuildResult_ShouldExposeMultipleNamespaces_ForV2Entry()
    {
        var entry = new ExtensionRegistryEntry
        {
            Verb = "deployment",
            Version = "1.0.0",
            Pinned = false,
            InstalledAt = DateTimeOffset.UtcNow,
            Contributions =
            {
                new Contribution { Verb = "deployment", Path = "group create" },
                new Contribution { Verb = "group", Path = "create" },
            },
            Namespaces = { "deployment", "group" },
        };

        var result = ShowExtensionProcessor.BuildResult(PackageId, entry);
        var type = result.GetType();

        Assert.Equal(PackageId, type.GetProperty("PackageId")!.GetValue(result));
        Assert.Equal("deployment", type.GetProperty("Verb")!.GetValue(result));
        var namespaces = AsList(type.GetProperty("Namespaces")!.GetValue(result));
        Assert.Equal(new[] { "deployment", "group" }, namespaces);
    }

    [Fact]
    public void BuildResult_ShouldFallBackToSingletonVerb_ForLegacyV1Entry()
    {
        // v1 entry: only `Verb` is populated; `Namespaces` is empty until NormalizeEntry runs.
        // BuildResult must still emit a non-empty Namespaces field so the output shape is stable.
        var entry = new ExtensionRegistryEntry
        {
            Verb = "group",
            Version = "1.0.0",
            InstalledAt = DateTimeOffset.UtcNow,
        };

        var result = ShowExtensionProcessor.BuildResult(PackageId, entry);
        var namespaces = AsList(result.GetType().GetProperty("Namespaces")!.GetValue(result));

        Assert.Equal(new[] { "group" }, namespaces);
    }

    [Fact]
    public void BuildResult_ShouldEmitEmptyNamespaces_WhenNeitherVerbNorNamespacesPresent()
    {
        var entry = new ExtensionRegistryEntry
        {
            Verb = null,
            Version = "1.0.0",
            InstalledAt = DateTimeOffset.UtcNow,
        };

        var result = ShowExtensionProcessor.BuildResult(PackageId, entry);
        var namespaces = AsList(result.GetType().GetProperty("Namespaces")!.GetValue(result));

        Assert.Empty(namespaces);
    }
}
