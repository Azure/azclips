// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Configuration.Abstractions;

public interface IConfigurationValidator
{
    ValidationResult ValidateKey(string key, bool force = false);

    ValidationResult ValidateValue(string key, object? value);

    ValidationResult ValidateOptions<T>(T options) where T : class;
}
