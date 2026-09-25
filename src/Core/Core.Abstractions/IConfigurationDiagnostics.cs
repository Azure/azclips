// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public interface IConfigurationDiagnostics
{
    Task<IReadOnlyList<ConfigurationDiagnostic>> GetDiagnosticsAsync(CancellationToken cancellationToken = default);
}

public sealed record ConfigurationDiagnostic(string Code, string Message);
