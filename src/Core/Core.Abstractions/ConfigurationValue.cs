// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public sealed record ConfigurationValue(string Key, object? Value, string Source);
