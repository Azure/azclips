// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public sealed record class ConfigurationKeyMetadata
{
    public required string Key { get; init; }

    public string? Description { get; init; }

    public string? DefaultValue { get; init; }

    public Type? ValueType { get; init; }

    public bool IsRequired { get; init; }

    public IReadOnlyList<string>? AllowedValues { get; init; }

    public string? ValidationPattern { get; init; }

    public int? Minimum { get; init; }

    public int? Maximum { get; init; }

    public bool IsUri { get; init; }

    public bool IsList { get; init; }
}
