// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO.Pipes;
using System.Text.Json;
using Core.Abstractions.Recovery;

namespace Core.Agent.AgentMode;

internal static class RecoveryInput
{
    internal static async Task<RecoveryRequest?> ReadAsync(string[] args, CancellationToken cancellationToken)
    {
        var index = Array.IndexOf(args, RecoveryRequest.InputPipeArgument);
        if (index < 0)
            return null;

        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) ||
            Array.LastIndexOf(args, RecoveryRequest.InputPipeArgument) != index ||
            args.Contains("--failed-command") || args.Contains("--error"))
        {
            throw new ArgumentException("Invalid recovery input handoff.");
        }

        using var pipe = new AnonymousPipeClientStream(PipeDirection.In, args[index + 1]);
        return await ReadRequestAsync(pipe, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<RecoveryRequest> ReadRequestAsync(Stream input, CancellationToken cancellationToken)
    {
        RecoveryRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<RecoveryRequest>(
                input, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The recovery input is not a valid request.", ex);
        }

        if (request is not { SchemaVersion: RecoveryRequest.CurrentSchemaVersion } ||
            !Enum.IsDefined(request.Trigger) ||
            request.FailedCommand is null || request.ErrorText is null)
        {
            throw new InvalidDataException("The recovery input has an unsupported or incomplete contract.");
        }
        if ((request.TenantId is not null && !Guid.TryParse(request.TenantId, out _)) ||
            request.DataBoundary is not (null or "EU" or "Global"))
        {
            throw new InvalidDataException("The recovery input contains invalid telemetry routing metadata.");
        }

        return request;
    }
}
