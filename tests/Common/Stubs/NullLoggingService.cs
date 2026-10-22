// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Tests.Common.Stubs;

/// <summary>
/// No-op <see cref="ILoggingService"/> for code that must construct a service outside the DI
/// container. The real <c>ScopedLoggingService</c> consults configuration and may emit fallback
/// diagnostics, while setup code using this stub must remain silent and configuration-independent.
/// </summary>
public sealed class NullLoggingService : ILoggingService
{
    public void Log(string message) { }

    public void Info(string message) { }

    public void Verbose(string message) { }

    public void Debug(string message) { }

    public void Warn(string message) { }

    public void Error(string message) { }
}
