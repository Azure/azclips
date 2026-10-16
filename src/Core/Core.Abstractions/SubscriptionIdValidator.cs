namespace Core.Abstractions;

/// <summary>Validation rules for Azure subscription IDs.</summary>
public static class SubscriptionIdValidator
{
    /// <summary>
    /// Validates a subscription ID, throwing a usage error (exit code 2) when invalid. The
    /// offending parameter name, when supplied, is carried in the error details.
    /// </summary>
    public static void Validate(string? subscriptionId, string? parameterName = null, bool allowEmpty = false)
    {
        if (allowEmpty && string.IsNullOrWhiteSpace(subscriptionId)) return;

        if (string.IsNullOrWhiteSpace(subscriptionId)
            || subscriptionId != subscriptionId.Trim()
            || !Guid.TryParse(subscriptionId, out _))
        {
            throw AzclipsErrorException.UsageError(
                parameterName is null
                    ? $"Invalid subscription id '{subscriptionId}'"
                    : $"Invalid subscription id '{subscriptionId}' for '{parameterName}'",
                new Dictionary<string, object?> { ["parameter"] = parameterName ?? "subscriptionId" });
        }
    }
}
