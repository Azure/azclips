// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Accounts.ParameterSets;

namespace Extensions.Accounts.Tests;

/// <summary>
/// Unit tests for parameter-set <b>definition</b> validation: the checks that prove a set of
/// definitions is well-formed and mutually exclusive before it is ever used to resolve input.
/// </summary>
public class ParameterSetDefinitionTests
{
    private sealed class Probe
    {
        public string A { get; set; } = "";

        public string B { get; set; } = "";

        public string C { get; set; } = "";

        public bool Flag { get; set; }
    }

    [Fact]
    public void Validate_DisjointSets_Passes()
    {
        var resolver = new ParameterSetResolver<Probe>();
        resolver.DefineSet("One").WithMandatory(p => p.A);
        resolver.DefineSet("Two").WithMandatory(p => p.B);

        resolver.Validate();
    }

    [Fact]
    public void Validate_SharedMandatoryWithOwnDiscriminator_Passes()
    {
        // Both require A, but each adds its own discriminating mandatory parameter.
        var resolver = new ParameterSetResolver<Probe>();
        resolver.DefineSet("Key").WithMandatory(p => p.A).WithMandatory(p => p.B);
        resolver.DefineSet("Sas").WithMandatory(p => p.A).WithMandatory(p => p.C);

        resolver.Validate();
    }

    [Fact]
    public void Validate_OneSidedDiscriminator_Passes()
    {
        // Mirrors Login's DeviceCode/Browser sets: Browser's mandatory parameters are a subset of
        // DeviceCode's allowed parameters, but DeviceCode's mandatory switch keeps the pair exclusive.
        var resolver = new ParameterSetResolver<Probe>();
        resolver.DefineSet("DeviceCode").WithMandatory(p => p.Flag).WithMandatory(p => p.A).WithMandatory(p => p.B);
        resolver.DefineSet("Browser").WithMandatory(p => p.A).WithMandatory(p => p.B);

        resolver.Validate();
    }

    [Fact]
    public void Resolve_OneSidedDiscriminator_PicksExactlyOneSet()
    {
        var resolver = new ParameterSetResolver<Probe>();
        resolver.DefineSet("DeviceCode").WithMandatory(p => p.Flag).WithMandatory(p => p.A).WithMandatory(p => p.B);
        resolver.DefineSet("Browser").WithMandatory(p => p.A).WithMandatory(p => p.B);

        Assert.Equal("DeviceCode", resolver.Resolve(new Probe { Flag = true, A = "a", B = "b" }).Name);
        Assert.Equal("Browser", resolver.Resolve(new Probe { Flag = false, A = "a", B = "b" }).Name);
    }

    [Fact]
    public void Validate_EmptyDefinition_IsAllowed()
    {
        var resolver = new ParameterSetResolver<Probe>();

        resolver.Validate();
    }

    [Fact]
    public void Validate_SetWithoutMandatory_Throws()
    {
        var resolver = new ParameterSetResolver<Probe>();
        resolver.DefineSet("Empty").WithOptional(p => p.A);

        var ex = Assert.Throws<InvalidParameterSetDefinitionException>(() => resolver.Validate());
        Assert.Contains("no mandatory parameter", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NonExclusiveSets_Throws()
    {
        // "Two" mandatory {A} is a subset of "One" allowed {A,B}, and "One" mandatory {A} is allowed
        // by "Two" {A}. Neither owns a discriminator, so the pair is ambiguous.
        var resolver = new ParameterSetResolver<Probe>();
        resolver.DefineSet("One").WithMandatory(p => p.A).WithOptional(p => p.B);
        resolver.DefineSet("Two").WithMandatory(p => p.A);

        var ex = Assert.Throws<InvalidParameterSetDefinitionException>(() => resolver.Validate());
        Assert.Contains("not mutually exclusive", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefineSet_DuplicateName_Throws()
    {
        var resolver = new ParameterSetResolver<Probe>();
        resolver.DefineSet("Dup").WithMandatory(p => p.A);

        Assert.Throws<InvalidParameterSetDefinitionException>(() => resolver.DefineSet("Dup"));
    }

    [Fact]
    public void WithMandatory_DuplicateParameterInSameSet_Throws()
    {
        var resolver = new ParameterSetResolver<Probe>();

        Assert.Throws<InvalidParameterSetDefinitionException>(
            () => resolver.DefineSet("One").WithMandatory(p => p.A).WithMandatory(p => p.A));
    }

    [Fact]
    public void WithMandatory_NonPropertySelector_Throws()
    {
        var resolver = new ParameterSetResolver<Probe>();

        Assert.Throws<ArgumentException>(
            () => resolver.DefineSet("One").WithMandatory(p => p.A + p.B));
    }

    [Fact]
    public void Resolve_WhenDefinitionIsAmbiguous_ReportsMultipleMatches()
    {
        // Intentionally skip Validate to exercise the multi-match branch of Resolve.
        var resolver = new ParameterSetResolver<Probe>();
        resolver.DefineSet("One").WithMandatory(p => p.A).WithOptional(p => p.B);
        resolver.DefineSet("Two").WithMandatory(p => p.A).WithOptional(p => p.C);

        var ex = Assert.Throws<ParameterSetResolutionException>(
            () => resolver.Resolve(new Probe { A = "x" }));
        Assert.Contains("ambiguous", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'One'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'Two'", ex.Message, StringComparison.Ordinal);
    }
}
