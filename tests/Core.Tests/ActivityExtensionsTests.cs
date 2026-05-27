// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions.Runtime;
using Core.Telemetry;

namespace Core.Tests;

public class ActivityExtensionsTests
{
    [Fact]
    public void GetRoot_ShouldReturnSelf_WhenActivityHasNoParent()
    {
        using var activity = new Activity("independent").Start();

        var root = activity.GetRoot();

        Assert.Same(activity, root);
    }

    [Fact]
    public void GetRoot_ShouldReturnParent_WhenActivityHasSingleParent()
    {
        using var parent = new Activity("parent").Start();
        using var child = new Activity("child").Start();

        var root = child.GetRoot();

        Assert.Same(parent, root);
        Assert.Same(parent, child.Parent);
    }

    [Fact]
    public void GetRoot_ShouldReturnTopmostAncestor_WhenActivityIsDeeplyNested()
    {
        using var root = new Activity("root").Start();
        using var middle = new Activity("middle").Start();
        using var leaf = new Activity("leaf").Start();

        var resolved = leaf.GetRoot();

        Assert.Same(root, resolved);
    }
}
