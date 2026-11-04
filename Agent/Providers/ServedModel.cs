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

    private string? _latest;
    private string? _turn;
    private string? _response;

    /// <summary>The last served model in this session, or null.</summary>
    internal string? Latest => Volatile.Read(ref _latest);

    /// <summary>The last served model since <see cref="BeginTurn"/>, or null.</summary>
    internal string? Turn => Volatile.Read(ref _turn);

    /// <summary>The served model of the most recent response, or null when it had no valid header.</summary>
    internal string? Response => Volatile.Read(ref _response);

    internal void BeginTurn() => Volatile.Write(ref _turn, null);

    /// <summary>
    /// Stores the header value of a response. A missing or unrecordable value clears
    /// <see cref="Response"/>.
    /// </summary>
    internal void Record(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var value = response.Headers.TryGetValues(HeaderName, out var values) ? values.FirstOrDefault() : null;
        if (RecoveryTelemetry.IsExportableModel(value))
        {
            Volatile.Write(ref _latest, value);
            Volatile.Write(ref _turn, value);
            Volatile.Write(ref _response, value);
            return;
        }

        Volatile.Write(ref _response, null);
    }
}
