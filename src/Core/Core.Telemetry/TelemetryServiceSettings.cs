// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry;

internal sealed record TelemetryServiceSettings(
    string ClientName,
    bool Enabled,
    bool OwnsProvider,
    ITelemetryActivityNameFilter NameFilter,
    ITelemetryActivityLifecycle Lifecycle);
