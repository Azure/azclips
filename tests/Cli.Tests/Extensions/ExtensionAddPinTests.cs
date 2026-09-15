// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Core.Abstractions;
using Core.Extensions;
using Microsoft.Azure.Azclips.Extensions.Extension.Processors;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// `extension add` must not silently overwrite a pinned entry. Covers the pin-precondition
/// helper extracted from <see cref="AddExtensionProcessor"/>.
/// </summary>
public class ExtensionAddPinTests
{
    private const string PackageId = "Microsoft.Azure.Azclips.Extensions.Resources";

    private static ExtensionRegistry RegistryWith(string version, bool pinned)
    {
        var registry = new ExtensionRegistry();
        registry.Extensions[PackageId] = new ExtensionRegistryEntry
        {
            Verb = "group",
            Version = version,
            Pinned = pinned,
            InstalledAt = DateTimeOffset.UtcNow,
        };
        return registry;
    }

    [Fact]
    public void CheckPinPrecondition_ShouldReturnFalse_WhenNoExistingEntry()
    {
        var registry = new ExtensionRegistry();

        var carry = AddExtensionProcessor.CheckPinPrecondition(registry, PackageId, "1.0.0", force: false, pin: false);

        Assert.False(carry);
    }

    [Fact]
    public void CheckPinPrecondition_ShouldReturnFalse_WhenExistingEntryIsNotPinned()
    {
        var registry = RegistryWith("1.0.0-alpha", pinned: false);

        var carry = AddExtensionProcessor.CheckPinPrecondition(registry, PackageId, "1.1.0-alpha", force: false, pin: false);

        Assert.False(carry);
    }

    [Fact]
    public void CheckPinPrecondition_ShouldThrowPinViolation_WhenPinnedAndNoForceOrPin()
    {
        var registry = RegistryWith("1.1.0-alpha", pinned: true);

        var ex = Assert.Throws<AzclipsErrorException>(
            () => AddExtensionProcessor.CheckPinPrecondition(registry, PackageId, "1.0.0-alpha", force: false, pin: false));

        Assert.Equal("PinViolation", ex.Code);
        Assert.Contains("pinned at version '1.1.0-alpha'", ex.Message);
        Assert.Contains("--force", ex.Message);
    }

    [Fact]
    public void CheckPinPrecondition_ShouldCarryPinForward_WhenForcedOverPin()
    {
        var registry = RegistryWith("1.1.0-alpha", pinned: true);

        var carry = AddExtensionProcessor.CheckPinPrecondition(registry, PackageId, "1.0.0-alpha", force: true, pin: false);

        Assert.True(carry);
    }

    [Fact]
    public void CheckPinPrecondition_ShouldCarryPinForward_WhenUserReassertsPin()
    {
        // User explicitly says `--pin` on the new install; that signals informed consent to
        // replace the pinned version, and the pin remains.
        var registry = RegistryWith("1.1.0-alpha", pinned: true);

        var carry = AddExtensionProcessor.CheckPinPrecondition(registry, PackageId, "1.0.0-alpha", force: false, pin: true);

        Assert.True(carry);
    }
}
