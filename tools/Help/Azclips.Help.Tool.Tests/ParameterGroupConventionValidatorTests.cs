// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azclips.Help.Tool.Validation;
using Core.Abstractions;

namespace Azclips.Help.Tool.Tests;

public class ParameterGroupConventionValidatorTests
{
    [Fact]
    public void ConformingGroupName_NoWarning()
    {
        var report = new ParameterGroupConventionValidator().Validate([typeof(ConformingProcessor)]);

        Assert.Empty(report.Warnings);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public void NonConformingGroupName_WarnsOnce()
    {
        var report = new ParameterGroupConventionValidator().Validate([typeof(NonConformingProcessor)]);

        var warning = Assert.Single(report.Warnings);
        Assert.Equal(ProblemIds.GRP_NonConventionalGroupName, warning.ProblemId);
        Assert.Contains("[ParameterGroup(\"Auth\")]", warning.Message);
        Assert.Contains("NonConformingProcessor.Token", warning.Message);
        Assert.Contains("\"Auth Arguments\"", warning.Message);
    }

    [Fact]
    public void PropertyWithoutAttribute_NoWarning()
    {
        var report = new ParameterGroupConventionValidator().Validate([typeof(UnannotatedProcessor)]);

        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void InheritedParameterGroupOnBaseClass_Walked()
    {
        // Regression guard: `Type.GetProperties(Public|Instance)` returns inherited public
        // properties, so a `[ParameterGroup]` on a base class must fire on the subtype scan.
        var report = new ParameterGroupConventionValidator().Validate([typeof(InheritingProcessor)]);

        var warning = Assert.Single(report.Warnings);
        Assert.Equal(ProblemIds.GRP_NonConventionalGroupName, warning.ProblemId);
        Assert.Contains("ProcessorWithBadBase.InheritedProperty", warning.Message);
    }

    [Fact]
    public void SameNonConformingAttributeAcrossMultipleSubtypes_ReportedOnce()
    {
        // Dedup: if the base class holds the annotation, we walk it once per subtype but the
        // report should only surface a single warning.
        var report = new ParameterGroupConventionValidator().Validate(
            [typeof(InheritingProcessor), typeof(AnotherInheritingProcessor)]);

        Assert.Single(report.Warnings);
    }

    // ── Test fixtures ──

    private sealed class ConformingProcessor
    {
        [ParameterGroup("Storage Arguments")]
        public string StorageAccount { get; set; } = string.Empty;
    }

    private sealed class NonConformingProcessor
    {
        [ParameterGroup("Auth")]
        public string Token { get; set; } = string.Empty;
    }

    private sealed class UnannotatedProcessor
    {
        public string Name { get; set; } = string.Empty;
    }

    private class ProcessorWithBadBase
    {
        [ParameterGroup("Managed Identity")]
        public string InheritedProperty { get; set; } = string.Empty;
    }

    private sealed class InheritingProcessor : ProcessorWithBadBase
    {
    }

    private sealed class AnotherInheritingProcessor : ProcessorWithBadBase
    {
    }
}
