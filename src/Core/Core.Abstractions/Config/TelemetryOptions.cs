// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

[ConfigurationSection("telemetry")]
public sealed class TelemetryOptions
{
    [ConfigurationKey("enabled", Description = "Enable or disable telemetry.")]
    public bool Enabled { get; set; } = true;
}
