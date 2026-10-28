namespace Core.Abstractions;

/// <summary>Validation rules for Azure subscription IDs.</summary>
public static class SubscriptionIdValidator
{
    /// <summary>Requires a subscription only when constructing a subscription-scoped operation.</summary>
    public static string Require(string? subscriptionId)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            throw AzclipsErrorException.MissingRequiredParameters(
                "This operation requires a subscription ID. Supply --subscription-id (CLI) or -SubscriptionId (PowerShell), or select a profile containing a subscription.",
                new Dictionary<string, object?> { ["parameter"] = "SubscriptionId" });
        }

        return subscriptionId;
    }

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
