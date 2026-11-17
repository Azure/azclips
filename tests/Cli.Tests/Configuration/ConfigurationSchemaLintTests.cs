// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Configuration.Schema;

namespace Cli.Tests;

public class ConfigurationSchemaLintTests
{
    [Fact]
    public void GetAllKeys_ShouldNotContainValueParentConflicts()
    {
        var keys = new ConfigurationSchema().GetAllKeys().Select(metadata => metadata.Key);
        var conflicts = FindValueParentConflicts(keys);

        Assert.True(conflicts.Count == 0, string.Join(Environment.NewLine, conflicts));
    }

    [Theory]
    [InlineData("something.configkey", "something.configkey.child")]
    [InlineData("something.configkey", "something.configkey.child.grandchild")]
    [InlineData("something.configkey.child", "something.configkey.child.grandchild")]
    [InlineData("Something.ConfigKey", "something.configkey.child")]
    [InlineData("something.configkey", "SOMETHING.CONFIGKEY.CHILD")]
    [InlineData("something", "something.configkey.child")]
    public void FindValueParentConflicts_ShouldRejectAncestors_RegardlessOfOrder(string parent, string child)
    {
        var expected = $"Configuration value key '{parent}' is also a parent namespace of '{child}'. Move its value to a child or sibling key.";

        Assert.Equal(expected, Assert.Single(FindValueParentConflicts([parent, child])));
        Assert.Equal(expected, Assert.Single(FindValueParentConflicts([child, parent])));
    }

    [Theory]
    [InlineData("something.configkey", "something.configkeyExtra.child")]
    [InlineData("something.configkey.child", "something.configkey.otherChild")]
    [InlineData("something.configkey.child", "something.configkey.childExtra")]
    [InlineData("something.configkey.child", "other.configkey.child")]
    public void FindValueParentConflicts_ShouldAllowUnrelatedValueKeys(string first, string second)
    {
        Assert.Empty(FindValueParentConflicts([first, second]));
        Assert.Empty(FindValueParentConflicts([second, first]));
    }

    [Fact]
    public void FindValueParentConflicts_ShouldAllowEmptySchema()
    {
        Assert.Empty(FindValueParentConflicts([]));
    }

    [Fact]
    public void FindValueParentConflicts_ShouldReportEveryConflictingAncestor()
    {
        var keys = new[] { "something.configkey.child", "something", "something.configkey" };
        var expected = new[]
        {
            "Configuration value key 'something' is also a parent namespace of 'something.configkey'. Move its value to a child or sibling key.",
            "Configuration value key 'something' is also a parent namespace of 'something.configkey.child'. Move its value to a child or sibling key.",
            "Configuration value key 'something.configkey' is also a parent namespace of 'something.configkey.child'. Move its value to a child or sibling key."
        };

        Assert.Equal(expected, FindValueParentConflicts(keys));
        Assert.Equal(expected, FindValueParentConflicts(keys.Reverse()));
    }

    private static IReadOnlyList<string> FindValueParentConflicts(IEnumerable<string> keys)
    {
        var keysByName = keys.Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(key => key, StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<string>();

        foreach (var child in keysByName.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            for (var separator = child.IndexOf('.'); separator >= 0; separator = child.IndexOf('.', separator + 1))
            {
                if (keysByName.TryGetValue(child[..separator], out var parent))
                {
                    conflicts.Add($"Configuration value key '{parent}' is also a parent namespace of '{child}'. Move its value to a child or sibling key.");
                }
            }
        }

        return conflicts;
    }
}
