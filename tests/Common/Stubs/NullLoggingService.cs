// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Tests.Common.Stubs;

/// <summary>
/// Logging service implementation that does nothing. Useful for tests that don't care about logging.
/// </summary>
public class NullLoggingService : ILoggingService
{
    public void Log(string message) { }
    public void Debug(string message) { }
    public void Error(string message) { }
    public void Info(string message) { }
    public void Verbose(string message) { }
    public void Warn(string message) { }
}
