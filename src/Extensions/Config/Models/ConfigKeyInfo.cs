// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Config.Models;

public sealed record ConfigKeyInfo(
    string Key,
    string? Description,
    string? DefaultValue,
    string? ValueType,
    IReadOnlyList<string>? AllowedValues,
    int? Minimum,
    int? Maximum,
    string? ValidationPattern,
    bool IsUri);
