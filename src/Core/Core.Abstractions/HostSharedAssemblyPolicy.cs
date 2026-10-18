// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public static class HostSharedAssemblyPolicy
{
    public static bool IsShared(string? assemblyName) =>
        assemblyName is not null &&
        (assemblyName.StartsWith("Core.", StringComparison.Ordinal) ||
         assemblyName.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal));
}
