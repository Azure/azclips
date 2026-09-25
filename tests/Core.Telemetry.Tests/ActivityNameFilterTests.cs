// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Core.Telemetry.Tests;

public class ActivityNameFilterTests
{
    private readonly ITelemetryActivityNameFilter _filter = new DefaultTelemetryActivityNameFilter();

    [Theory]
    [InlineData("CLI.Activity.Root")]
    [InlineData("PowerShell.Activity.Root")]
    [InlineData("TUI.Activity.Root")]
    [InlineData("AzureCLI.ProcessCommand")]
    [InlineData("AzureCLI.Startup")]
    [InlineData("PowerShell.Startup")]
    [InlineData("SessionCreated")]
    [InlineData("AuthBeforeTokenAcquisition")]
    [InlineData("ProcessorProcessAsyncStart")]
    public void IsAllowed_ExactNameInAllowlist_IsAllowed(string name)
    {
        Assert.True(_filter.IsAllowed(name));
    }

    [Theory]
    [InlineData("ProfileService.GetActiveProfileAsync")]
    [InlineData("AddExtensionProcessor.Process")]
    [InlineData("RemoveExtensionProcessor.Process")]
    [InlineData("UpdateExtensionProcessor.Process")]
    [InlineData("ShowExtensionProcessor.Process")]
    [InlineData("ListExtensionsProcessor.Process")]
    [InlineData("LoginProcessor.Process")]
    [InlineData("LogoutProcessor.Process")]
    [InlineData("ProfileListProcessor.Process")]
    [InlineData("SetConfigCommand.ProcessAsync")]
    [InlineData("GetConfigCommand.ProcessAsync")]
    [InlineData("ListConfigCommand.ProcessAsync")]
    [InlineData("DeleteConfigCommand.ProcessAsync")]
    [InlineData("InfoConfigCommand.ProcessAsync")]
    [InlineData("ActionStart.MyClass.MyMethod")]
    [InlineData("AzureCLI.Startup.Container")]
    [InlineData("PowerShell.Startup.Container")]
    public void IsAllowed_NameMatchingAllowedPrefix_IsAllowed(string name)
    {
        Assert.True(_filter.IsAllowed(name));
    }

    [Theory]
    [InlineData("System.Net.Http.HttpRequestOut")] // HTTP auto-instrumentation — deliberately dropped
    [InlineData("SomeUnknownActivity")]
    [InlineData("Activity.Root")]                  // partial match, not on allowlist
    [InlineData("FooBar.Process")]                 // looks like prefix pattern but isn't on the prefix list
    [InlineData("SessionDisposed")]                // end events don't create activities; only Start side is on the list
    [InlineData("ActionEnd")]
    [InlineData("AuthAfterTokenAcquisition")]
    [InlineData("MsalAuthService")]                // MsalAuthService
    [InlineData("MsalAuthServiceX.Method")]        // substring
    [InlineData("Agent.NotARealSpan")]             // Agent.* is exact-matched, not a prefix
    [InlineData("CLI.Activity.AiRecoverySkipped.Other")]
    [InlineData("toolbox.something")]              // "tool." prefix must not match "toolbox."
    [InlineData("CLI.Activity.AiRecovery")]
    [InlineData("CLI.Activity.AiRecoverySkipped")]
    [InlineData("Agent.Session")]
    [InlineData("Agent.Create")]
    [InlineData("Agent.Turn")]
    [InlineData("Agent.Send")]
    [InlineData("Agent.Reprompt")]
    [InlineData("Agent.DataBoundary")]
    [InlineData("tool.get_command")]
    [InlineData("tool.bash")]
    [InlineData("tool.")]
    public void IsAllowed_NameNotInAllowlist_IsDropped(string name)
    {
        Assert.False(_filter.IsAllowed(name));
    }

    [Theory]
    [InlineData("cli.activity.root")]              // wrong case — Ordinal matching, no fuzzy
    [InlineData("CLI.ACTIVITY.ROOT")]
    [InlineData("msalauthservice.LoginAsync")]
    [InlineData("ACTIONSTART.X.Y")]
    public void IsAllowed_IsCaseSensitive(string name)
    {
        Assert.False(_filter.IsAllowed(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsAllowed_NullOrEmpty_IsFalse(string? name)
    {
        Assert.False(_filter.IsAllowed(name));
    }

    [Fact]
    public void AllowlistExact_ExposesEveryAllowedExactName()
    {
        var exact = DefaultTelemetryActivityNameFilter.AllowlistExact;
        Assert.NotEmpty(exact);
        foreach (var name in exact)
        {
            Assert.True(_filter.IsAllowed(name), $"exact allowlist entry '{name}' should pass IsAllowed");
        }
    }

    [Fact]
    public void AllowlistPrefixes_ExposesEveryAllowedPrefix()
    {
        var prefixes = DefaultTelemetryActivityNameFilter.AllowlistPrefixes;
        Assert.NotEmpty(prefixes);
        foreach (var prefix in prefixes)
        {
            // A name composed of "<prefix>X" must pass; "<prefix-without-dot>" should not.
            Assert.True(_filter.IsAllowed(prefix + "AnyMethod"),
                $"prefix '{prefix}' should match name '{prefix}AnyMethod'");
        }
    }
}
