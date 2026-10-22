// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Accounts.ParameterSets;

namespace Extensions.Accounts.Tests;

/// <summary>
/// Unit tests for the internal parameter-set utilities
/// (<see cref="ParameterSetResolver{TCommand}"/> and friends). Covers definition validation
/// (mutual-exclusivity proof) and input resolution, including the actionable error messages produced
/// when zero or multiple sets match.
/// </summary>
public class ParameterSetResolverTests
{
    /// <summary>A stand-in command modeled on a login-style processor: parameters are properties and a
    /// parameter is "supplied" when non-null / non-empty / a true switch.</summary>
    private sealed class FakeLoginCommand
    {
        public string? ConnectionString { get; set; }

        public string AccountName { get; set; } = "";

        public string AccountKey { get; set; } = "";

        public string SasToken { get; set; } = "";

        public string Endpoint { get; set; } = "";

        public bool UseDeviceCode { get; set; }
    }

    private static ParameterSetResolver<FakeLoginCommand> BuildResolver()
    {
        var resolver = new ParameterSetResolver<FakeLoginCommand>();

        resolver.DefineSet("ConnectionString")
            .WithMandatory(c => c.ConnectionString);

        resolver.DefineSet("AccountKey")
            .WithMandatory(c => c.AccountName)
            .WithMandatory(c => c.AccountKey)
            .WithOptional(c => c.Endpoint);

        resolver.DefineSet("Sas")
            .WithMandatory(c => c.AccountName)
            .WithMandatory(c => c.SasToken)
            .WithOptional(c => c.Endpoint);

        return resolver;
    }

    [Fact]
    public void Validate_MutuallyExclusiveDefinition_DoesNotThrow()
    {
        BuildResolver().Validate();
    }

    [Fact]
    public void Resolve_ConnectionStringOnly_ResolvesConnectionStringSet()
    {
        var resolver = BuildResolver();
        var cmd = new FakeLoginCommand { ConnectionString = "conn" };

        Assert.Equal("ConnectionString", resolver.Resolve(cmd).Name);
    }

    [Fact]
    public void Resolve_AccountKeyWithOptionalEndpoint_ResolvesAccountKeySet()
    {
        var resolver = BuildResolver();
        var cmd = new FakeLoginCommand { AccountName = "a", AccountKey = "k", Endpoint = "e" };

        Assert.Equal("AccountKey", resolver.Resolve(cmd).Name);
    }

    [Fact]
    public void Resolve_Sas_ResolvesSasSet()
    {
        var resolver = BuildResolver();
        var cmd = new FakeLoginCommand { AccountName = "a", SasToken = "s" };

        Assert.Equal("Sas", resolver.Resolve(cmd).Name);
    }

    [Fact]
    public void Resolve_NothingSupplied_ThrowsNoMatch()
    {
        var resolver = BuildResolver();

        var ex = Assert.Throws<ParameterSetResolutionException>(() => resolver.Resolve(new FakeLoginCommand()));
        Assert.Contains("do not match any parameter set", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_IncompleteMandatory_ReportsMissingParameter()
    {
        var resolver = BuildResolver();
        var cmd = new FakeLoginCommand { AccountName = "a" }; // AccountKey / SasToken missing

        var ex = Assert.Throws<ParameterSetResolutionException>(() => resolver.Resolve(cmd));
        Assert.Contains("missing mandatory", ex.Message, StringComparison.Ordinal);
        Assert.Contains("AccountKey", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_MixingSets_ReportsNotAllowedParameter()
    {
        var resolver = BuildResolver();
        var cmd = new FakeLoginCommand { ConnectionString = "conn", AccountKey = "k" };

        var ex = Assert.Throws<ParameterSetResolutionException>(() => resolver.Resolve(cmd));
        Assert.Contains("does not allow", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolve_Success_ReturnsTrueAndSet()
    {
        var resolver = BuildResolver();
        var cmd = new FakeLoginCommand { ConnectionString = "conn" };

        bool ok = resolver.TryResolve(cmd, out var set, out var error);

        Assert.True(ok);
        Assert.NotNull(set);
        Assert.Equal("ConnectionString", set!.Name);
        Assert.Null(error);
    }

    [Fact]
    public void TryResolve_Failure_ReturnsFalseAndMessage()
    {
        var resolver = BuildResolver();

        bool ok = resolver.TryResolve(new FakeLoginCommand(), out var set, out var error);

        Assert.False(ok);
        Assert.Null(set);
        Assert.NotNull(error);
    }

    [Fact]
    public void BoolSwitch_CountsAsSuppliedOnlyWhenTrue()
    {
        var resolver = new ParameterSetResolver<FakeLoginCommand>();
        resolver.DefineSet("Device").WithMandatory(c => c.UseDeviceCode);
        resolver.DefineSet("ConnectionString").WithMandatory(c => c.ConnectionString);

        resolver.Validate();

        Assert.Equal("Device", resolver.Resolve(new FakeLoginCommand { UseDeviceCode = true }).Name);

        // A false switch is not "supplied", so it does not select the Device set.
        var ex = Assert.Throws<ParameterSetResolutionException>(
            () => resolver.Resolve(new FakeLoginCommand { UseDeviceCode = false }));
        Assert.Contains("do not match any parameter set", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void WhitespaceString_CountsAsSupplied_ByDefault(string value)
    {
        var resolver = new ParameterSetResolver<FakeLoginCommand>();
        resolver.DefineSet("Key").WithMandatory(c => c.AccountKey);
        resolver.DefineSet("ConnectionString").WithMandatory(c => c.ConnectionString);
        resolver.Validate();

        Assert.Equal("Key", resolver.Resolve(new FakeLoginCommand { AccountKey = value }).Name);
        Assert.Throws<ParameterSetResolutionException>(() =>
            resolver.Resolve(new FakeLoginCommand { AccountKey = value, ConnectionString = "conn" }));
    }

    [Fact]
    public void CustomSuppliedPredicate_IsHonored()
    {
        var resolver = new ParameterSetResolver<FakeLoginCommand>();

        // Treat any non-null endpoint (even empty) as supplied, overriding the default string rule.
        resolver.DefineSet("Endpoint").WithMandatory(c => c.Endpoint, isSupplied: v => v is not null);
        resolver.DefineSet("ConnectionString").WithMandatory(c => c.ConnectionString);
        resolver.Validate();

        Assert.Equal("Endpoint", resolver.Resolve(new FakeLoginCommand { Endpoint = "" }).Name);
    }
}
