// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Telemetry;

namespace Core.Agent.Providers;

/// <summary>
/// Holds the model that the builtin gateway reports in the <c>x-ms-served-model</c> response
/// header for one session.
/// </summary>
internal sealed class ServedModel
{
    internal const string HeaderName = "x-ms-served-model";

    private readonly Action<string> _reportWarning;
    private string? _latest;
    private string? _turn;
    private int _warned;

    internal ServedModel(Action<string> reportWarning) => _reportWarning = reportWarning;

    /// <summary>The last served model in this session, or null.</summary>
    internal string? Latest => Volatile.Read(ref _latest);

    /// <summary>The last served model since <see cref="BeginTurn"/>, or null.</summary>
    internal string? Turn => Volatile.Read(ref _turn);

    internal void BeginTurn() => Volatile.Write(ref _turn, null);

    /// <summary>
    /// Stores the header value of a response. A missing or unrecordable value
    /// reports one warning per session.
    /// </summary>
    internal void Record(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var value = response.Headers.TryGetValues(HeaderName, out var values) ? values.FirstOrDefault() : null;
        if (RecoveryTelemetry.IsExportableModel(value))
        {
            Volatile.Write(ref _latest, value);
            Volatile.Write(ref _turn, value);
            return;
        }

        if (Interlocked.Exchange(ref _warned, 1) != 0)
            return;
        try
        {
            _reportWarning($"The model service did not report a valid {HeaderName} header. Telemetry and trace use the configured model name.");
        }
        catch
        {
        }
    }
}
