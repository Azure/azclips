using Core.Abstractions;
using Xunit;

namespace Cli.Tests;

/// <summary>Tests managed identity key formatting and parsing.</summary>
public sealed class ManagedIdentityIdCodecTests
{
    [Theory]
    [InlineData(ManagedIdentityIdType.ClientId, "11111111-1111-1111-1111-111111111111")]
    [InlineData(ManagedIdentityIdType.ObjectId, "22222222-2222-2222-2222-222222222222")]
    [InlineData(ManagedIdentityIdType.ResourceId, "/subscriptions/s/resourceGroups/r/providers/Microsoft.ManagedIdentity/userAssignedIdentities/u")]
    public void FormatThenParse_ShouldRoundTrip_ForUserAssigned(ManagedIdentityIdType idType, string idValue)
    {
        var combined = ManagedIdentityIdCodec.Format(idType, idValue);

        var (parsedType, parsedValue) = ManagedIdentityIdCodec.Parse(combined);

        Assert.Equal(idType, parsedType);
        Assert.Equal(idValue, parsedValue);
    }

    [Fact]
    public void FormatThenParse_ShouldRoundTrip_ForSystemAssigned()
    {
        var combined = ManagedIdentityIdCodec.Format(ManagedIdentityIdType.SystemAssigned, null);
        Assert.Equal("MSI@", combined);

        var (parsedType, parsedValue) = ManagedIdentityIdCodec.Parse(combined);

        Assert.Equal(ManagedIdentityIdType.SystemAssigned, parsedType);
        Assert.Null(parsedValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("MSI@")]
    [InlineData("no-at-sign")]
    public void ParseManagedIdentityId_ShouldReturnSystemAssigned_ForEmptyOrUnkeyedInput(string? combinedId)
    {
        var (parsedType, parsedValue) = ManagedIdentityIdCodec.Parse(combinedId);

        Assert.Equal(ManagedIdentityIdType.SystemAssigned, parsedType);
        Assert.Null(parsedValue);
    }

    [Fact]
    public void ParseManagedIdentityId_ShouldThrow_ForUnknownPrefix()
    {
        Assert.Throws<System.ArgumentException>(
            () => ManagedIdentityIdCodec.Parse("MSIBogus@value"));
    }

    [Fact]
    public void ParseManagedIdentityId_ShouldYieldEmptyValue_WhenUserAssignedValueMissing()
    {
        // empty user-assigned ids parse, then fail credential validation
        var (parsedType, parsedValue) = ManagedIdentityIdCodec.Parse("MSIClient@");

        Assert.Equal(ManagedIdentityIdType.ClientId, parsedType);
        Assert.Equal(string.Empty, parsedValue);
    }

    [Theory]
    [InlineData(ManagedIdentityIdType.ClientId)]
    [InlineData(ManagedIdentityIdType.ObjectId)]
    [InlineData(ManagedIdentityIdType.ResourceId)]
    public void FormatManagedIdentityId_FromLoginRequest_ShouldPreserveTheCredentialSelector(ManagedIdentityIdType idType)
    {
        const string value = "abc-123";
        var request = new LoginRequest(null, null, new AuthCredential.ManagedIdentity(idType, value));

        var combined = ManagedIdentityIdCodec.Format(request);

        var (parsedType, parsedValue) = ManagedIdentityIdCodec.Parse(combined);
        Assert.Equal(idType, parsedType);
        Assert.Equal(value, parsedValue);
    }

    [Fact]
    public void FormatManagedIdentityId_FromLoginRequest_ShouldDefaultToSystemAssigned_WhenNoIdSet()
    {
        var combined = ManagedIdentityIdCodec.Format(
            new LoginRequest(null, null, new AuthCredential.ManagedIdentity(ManagedIdentityIdType.SystemAssigned)));

        Assert.Equal("MSI@", combined);
    }
}
