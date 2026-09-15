namespace Core.Abstractions;

/// <summary>Validation rules for Azure subscription IDs.</summary>
public static class SubscriptionIdValidator
{
    /// <summary>
    /// Validates a subscription ID, throwing <see cref="ArgumentException"/> when invalid.
    /// </summary>
    public static void Validate(string? subscriptionId, string? parameterName = null, bool allowEmpty = false)
    {
        if (allowEmpty && string.IsNullOrWhiteSpace(subscriptionId)) return;

        if (string.IsNullOrWhiteSpace(subscriptionId)
            || subscriptionId != subscriptionId.Trim()
            || !Guid.TryParse(subscriptionId, out _))
        {
            throw new ArgumentException(
                $"Invalid subscription id '{subscriptionId}'",
                parameterName ?? nameof(subscriptionId));
        }
    }
}
