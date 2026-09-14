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

    [Fact]
    public void FormatThenParse_ShouldIgnoreTheRecordedId_ForSystemAssigned()
    {
        // the id identifies the signed-in identity but must not be handed to the credential,
        // which selects the system-assigned identity itself
        const string clientId = "33333333-3333-3333-3333-333333333333";
        var combined = ManagedIdentityIdCodec.Format(ManagedIdentityIdType.SystemAssigned, clientId);
        Assert.Equal($"MSI@{clientId}", combined);

        var (parsedType, parsedValue) = ManagedIdentityIdCodec.Parse(combined);

        Assert.Equal(ManagedIdentityIdType.SystemAssigned, parsedType);
        Assert.Null(parsedValue);
    }

    [Fact]
    public void FormatManagedIdentityId_FromLoginRequest_ShouldRecordTheSystemAssignedId()
    {
        const string clientId = "44444444-4444-4444-4444-444444444444";

        var combined = ManagedIdentityIdCodec.Format(new LoginRequest(), clientId);

        Assert.Equal($"MSI@{clientId}", combined);
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
    public void FormatManagedIdentityId_FromLoginRequest_ShouldSelectTheSetField(ManagedIdentityIdType idType)
    {
        const string value = "abc-123";
        var request = idType switch
        {
            ManagedIdentityIdType.ClientId => new LoginRequest { ManagedIdentityClientId = value },
            ManagedIdentityIdType.ObjectId => new LoginRequest { ManagedIdentityObjectId = value },
            _ => new LoginRequest { ManagedIdentityResourceId = value },
        };

        var combined = ManagedIdentityIdCodec.Format(request);

        var (parsedType, parsedValue) = ManagedIdentityIdCodec.Parse(combined);
        Assert.Equal(idType, parsedType);
        Assert.Equal(value, parsedValue);
    }

    [Fact]
    public void FormatManagedIdentityId_FromLoginRequest_ShouldDefaultToSystemAssigned_WhenNoIdSet()
    {
        var combined = ManagedIdentityIdCodec.Format(new LoginRequest());

        Assert.Equal("MSI@", combined);
    }
}
