// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Core.Authentication;
using Xunit;

namespace Cli.Tests;

/// <summary>Tests reading the identity claims carried by an access token.</summary>
public sealed class AccessTokenClaimsTests
{
    private const string TenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";
    private const string AppId = "b4c5d6e7-1234-4abc-8def-0123456789ab";
    private const string ObjectId = "22222222-2222-2222-2222-222222222222";

    private static string BuildJwt(string payloadJson)
    {
        static string B64Url(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return $"{B64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}")}.{B64Url(payloadJson)}.{B64Url("signature")}";
    }

    [Fact]
    public void Read_ShouldReturnTenantApplicationAndObjectIds()
    {
        var token = BuildJwt($"{{\"tid\":\"{TenantId}\",\"appid\":\"{AppId}\",\"oid\":\"{ObjectId}\"}}");

        var claims = AccessTokenClaims.Read(token);

        Assert.Equal(TenantId, claims.TenantId);
        Assert.Equal(AppId, claims.ClientId);
        Assert.Equal(ObjectId, claims.ObjectId);
    }

    [Fact]
    public void Read_ShouldFallBackToAuthorizedParty_ForV2Tokens()
    {
        var token = BuildJwt($"{{\"tid\":\"{TenantId}\",\"azp\":\"{AppId}\"}}");

        var claims = AccessTokenClaims.Read(token);

        Assert.Equal(AppId, claims.ClientId);
        Assert.Null(claims.ObjectId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    public void Read_ShouldReturnEmptyClaims_ForUnreadableTokens(string? token)
    {
        var claims = AccessTokenClaims.Read(token);

        Assert.Null(claims.TenantId);
        Assert.Null(claims.ClientId);
        Assert.Null(claims.ObjectId);
    }
}
