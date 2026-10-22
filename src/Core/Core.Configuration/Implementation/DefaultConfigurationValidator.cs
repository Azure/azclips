// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Core.Abstractions;
using Core.Configuration.Abstractions;

namespace Core.Configuration.Implementation;

public sealed class DefaultConfigurationValidator(IConfigurationSchema schema) : IConfigurationValidator
{
    private readonly IConfigurationSchema _schema = schema;

    public ValidationResult ValidateKey(string key, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return ValidationResult.Failure("Configuration key cannot be empty.");
        }

        if (_schema.IsKnownKey(key) || force)
        {
            return ValidationResult.Success();
        }

        return ValidationResult.Failure($"Unknown configuration key '{key}'. Use the force option to allow experimental keys.");
    }

    public ValidationResult ValidateValue(string key, object? value)
    {
        var keyValidation = ValidateKey(key, force: true);
        if (!keyValidation.IsValid)
        {
            return keyValidation;
        }

        var metadata = _schema.GetKeyMetadata(key);
        if (metadata is null)
        {
            return ValidationResult.Success();
        }

        if (value is null)
        {
            return metadata.IsRequired
                ? ValidationResult.Failure($"Value for '{key}' cannot be null.")
                : ValidationResult.Success();
        }

        if (metadata.ValueType is not null && !IsCompatibleType(metadata.ValueType, value))
        {
            return ValidationResult.Failure($"Value for '{key}' must be of type '{metadata.ValueType.Name}'.");
        }

        if (metadata.AllowedValues is { Count: > 0 })
        {
            var valueText = Convert.ToString(value);
            if (!metadata.AllowedValues.Contains(valueText ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                return ValidationResult.Failure(
                    $"Value '{valueText}' is not allowed for '{key}'. Allowed values: {string.Join(", ", metadata.AllowedValues)}.");
            }
        }

        if (metadata.ValidationPattern is not null)
        {
            var valueText = Convert.ToString(value) ?? string.Empty;
            if (!Regex.IsMatch(valueText, metadata.ValidationPattern))
            {
                return ValidationResult.Failure($"Value '{valueText}' does not match expected pattern for '{key}'.");
            }
        }

        if (metadata.Minimum.HasValue || metadata.Maximum.HasValue)
        {
            if (!TryConvertToInt(value, out var intValue))
            {
                return ValidationResult.Failure($"Value for '{key}' must be numeric.");
            }

            if (metadata.Minimum.HasValue && intValue < metadata.Minimum.Value)
            {
                return ValidationResult.Failure($"Value for '{key}' cannot be less than {metadata.Minimum.Value}.");
            }

            if (metadata.Maximum.HasValue && intValue > metadata.Maximum.Value)
            {
                return ValidationResult.Failure($"Value for '{key}' cannot be greater than {metadata.Maximum.Value}.");
            }
        }

        if (metadata.IsUri)
        {
            var valueText = Convert.ToString(value);
            if (!string.IsNullOrWhiteSpace(valueText) && !Uri.TryCreate(valueText, UriKind.Absolute, out _))
            {
                return ValidationResult.Failure($"Value '{valueText}' is not a valid absolute URI for '{key}'.");
            }
        }

        return ValidationResult.Success();
    }

    public ValidationResult ValidateOptions<T>(T options) where T : class
    {
        if (options is null)
        {
            return ValidationResult.Failure("Options object cannot be null.");
        }

        return ValidationResult.Success();
    }

    private static bool IsCompatibleType(Type expectedType, object value)
    {
        if (expectedType.IsInstanceOfType(value))
        {
            return true;
        }

        if (expectedType == typeof(int))
        {
            return TryConvertToInt(value, out _);
        }

        if (expectedType == typeof(bool))
        {
            return bool.TryParse(Convert.ToString(value), out _);
        }

        if (expectedType == typeof(string))
        {
            return value is string;
        }

        return false;
    }

    private static bool TryConvertToInt(object value, out int intValue)
    {
        if (value is int alreadyInt)
        {
            intValue = alreadyInt;
            return true;
        }

        return int.TryParse(Convert.ToString(value), out intValue);
    }
}
