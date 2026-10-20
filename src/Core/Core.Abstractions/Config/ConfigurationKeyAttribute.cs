// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ConfigurationKeyAttribute(string key) : Attribute
{
    public string Key { get; } = key;

    /// <summary>Optional public key when the typed/storage path differs from the user-facing name.</summary>
    public string? PublicKey { get; init; }

    public string? Description { get; init; }

    public string[]? AllowedValues { get; init; }

    public string? ValidationPattern { get; init; }

    public bool IsUri { get; init; }

    public bool IsRequired { get; init; }

    public bool IncludeDefault { get; init; } = true;

    public int Minimum { get; init; } = int.MinValue;

    public int Maximum { get; init; } = int.MaxValue;
}
