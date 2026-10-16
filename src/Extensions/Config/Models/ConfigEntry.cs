// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Config.Models;

public sealed record ConfigEntry(string Key, object? Value, string Source);
