using Core.Abstractions;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Unit tests for <see cref="AuthContext"/>'s managed-identity combined-key helpers
/// (<see cref="AuthContext.FormatManagedIdentityId(ManagedIdentityIdType, string?)"/> /
/// <see cref="AuthContext.ParseManagedIdentityId(string?)"/>). These encode the MI identity into
/// the "{MSIType}@{value}" key persisted in context.json and decode it for silent token refresh
/// (<see cref="ManagedIdentityAuthenticator.AcquireTokenAsync"/>), so a broken round-trip would
/// silently change or drop the identity used to re-acquire tokens.
/// </summary>
public sealed class AuthContextManagedIdentityIdTests
{
    [Theory]
    [InlineData(ManagedIdentityIdType.ClientId, "11111111-1111-1111-1111-111111111111")]
    [InlineData(ManagedIdentityIdType.ObjectId, "22222222-2222-2222-2222-222222222222")]
    [InlineData(ManagedIdentityIdType.ResourceId, "/subscriptions/s/resourceGroups/r/providers/Microsoft.ManagedIdentity/userAssignedIdentities/u")]
    public void FormatThenParse_ShouldRoundTrip_ForUserAssigned(ManagedIdentityIdType idType, string idValue)
    {
        var combined = AuthContext.FormatManagedIdentityId(idType, idValue);

        var (parsedType, parsedValue) = AuthContext.ParseManagedIdentityId(combined);

        Assert.Equal(idType, parsedType);
        Assert.Equal(idValue, parsedValue);
    }

    [Fact]
    public void FormatThenParse_ShouldRoundTrip_ForSystemAssigned()
    {
        var combined = AuthContext.FormatManagedIdentityId(ManagedIdentityIdType.SystemAssigned, null);
        Assert.Equal("MSI@", combined);

        var (parsedType, parsedValue) = AuthContext.ParseManagedIdentityId(combined);

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
        var (parsedType, parsedValue) = AuthContext.ParseManagedIdentityId(combinedId);

        Assert.Equal(ManagedIdentityIdType.SystemAssigned, parsedType);
        Assert.Null(parsedValue);
    }

    [Fact]
    public void ParseManagedIdentityId_ShouldThrow_ForUnknownPrefix()
    {
        Assert.Throws<System.ArgumentException>(
            () => AuthContext.ParseManagedIdentityId("MSIBogus@value"));
    }

    [Fact]
    public void ParseManagedIdentityId_ShouldYieldEmptyValue_WhenUserAssignedValueMissing()
    {
        // A tampered/corrupt context.json (e.g. "MSIClient@") decodes to a user-assigned type
        // with an empty id. ManagedIdentityCredential's constructor rejects that combination,
        // so this documents the exact input that triggers that guard.
        var (parsedType, parsedValue) = AuthContext.ParseManagedIdentityId("MSIClient@");

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

        var combined = AuthContext.FormatManagedIdentityId(request);

        var (parsedType, parsedValue) = AuthContext.ParseManagedIdentityId(combined);
        Assert.Equal(idType, parsedType);
        Assert.Equal(value, parsedValue);
    }

    [Fact]
    public void FormatManagedIdentityId_FromLoginRequest_ShouldDefaultToSystemAssigned_WhenNoIdSet()
    {
        var combined = AuthContext.FormatManagedIdentityId(new LoginRequest());

        Assert.Equal("MSI@", combined);
    }
}
