using Core.Abstractions;

namespace Cli.Tests;

public class SubscriptionIdValidatorTests
{
    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Validate_ShouldAccept_GuidSubscriptionIds(string subscriptionId)
    {
        SubscriptionIdValidator.Validate(subscriptionId);
    }

    [Theory]
    [InlineData("sub-1")]
    [InlineData(" 11111111-1111-1111-1111-111111111111 ")]
    [InlineData("Steven Ma [stema] subscription")]
    public void Validate_ShouldReject_NonGuidSubscriptionIds(string subscriptionId)
    {
        Assert.Throws<ArgumentException>(() => SubscriptionIdValidator.Validate(subscriptionId));
    }

    [Fact]
    public void Validate_ShouldThrow_OnNonGuidSubscriptionId()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            SubscriptionIdValidator.Validate("this is an invalid subscription id"));

        Assert.Contains("Invalid subscription id 'this is an invalid subscription id'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldAllow_EmptySubscriptionId_WhenAllowEmptyIsTrue()
    {
        SubscriptionIdValidator.Validate("", allowEmpty: true);
    }
}
