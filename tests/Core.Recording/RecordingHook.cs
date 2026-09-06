// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Recording;

/// <summary>
/// Lets a real az.exe or ps module process take part in record/playback scenario tests without
/// referencing test code. Inert unless <see cref="RecordingPathVariable"/> is set; the client
/// composition roots call <see cref="TryRegister"/> once, before container initialization.
/// <para>
/// A scripted scenario is one process per command, so state crosses processes through files:
/// record mode appends each captured exchange to the recording as it happens, and playback
/// tracks consumed entries in a ledger next to the recording so a later command's process
/// cannot re-serve an earlier command's response.
/// </para>
/// </summary>
public static class RecordingHook
{
    /// <summary>Path of the recording to write (record) or serve (playback). Setting it activates the hook.</summary>
    public const string RecordingPathVariable = "AZCLIPS_TEST_RECORDING";

    public static void TryRegister()
    {
        var path = Environment.GetEnvironmentVariable(RecordingPathVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var mode = RecorderModeResolver.FromEnvironment();
        if (mode == RecorderMode.Playback)
        {
            var record = SessionRecord.Load(path);
            var ledger = new ConsumptionLedger(path + ".consumed");
            var interceptor = new RecordPlaybackInterceptor(
                RecorderMode.Playback, record, alreadyConsumed: ledger.Read(), onConsumed: ledger.Append);

            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IHttpTerminateHandler>(interceptor);
                // Token acquisition happens in the processor before the pipeline runs, so
                // playback still has to answer it locally.
                services.AddSingleton<IAuthService, PlaybackAuthService>();
            });
        }
        else
        {
            var record = File.Exists(path) ? SessionRecord.Load(path) : new SessionRecord();
            var interceptor = new RecordPlaybackInterceptor(
                RecorderMode.Record, record, onRecorded: _ => record.Save(path));

            AzclipsServiceProvider.OverrideRegistration(services =>
                services.AddSingleton<IHttpTerminateHandler>(interceptor));
        }
    }
}
