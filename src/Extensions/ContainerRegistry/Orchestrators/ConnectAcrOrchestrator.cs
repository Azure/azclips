// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

/// <summary>
/// Orchestrator for ACR connect (docker login). Inherits from <see cref="AcrDataPlaneProcessorBase"/>
/// but overrides the entire flow since connect invokes local docker exe instead of HTTP.
/// </summary>
internal sealed class ConnectAcrOrchestrator
{
    private readonly IAuthHandler _authHandler;
    private readonly IRuntimeHttpPipeline _pipeline;
    private readonly IEventPublisher _publisher;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConnectAcrOrchestrator(
        IAuthHandler authHandler,
        IRuntimeHttpPipeline pipeline,
        IEventPublisher publisher,
        JsonSerializerOptions jsonOptions)
    {
        _authHandler = authHandler;
        _pipeline = pipeline;
        _publisher = publisher;
        _jsonOptions = jsonOptions;
    }

    /// <summary>
    /// Performs docker login to the specified registry.
    /// Flow: AAD token → ACR refresh token → docker login --password-stdin.
    /// </summary>
    public async Task<ConnectResult> ConnectAsync(string registryName)
    {
        string loginServer = AcrDataPlaneProcessorBase.ResolveLoginServerFromName(registryName);

        // Acquire ACR refresh token (used as docker login password)
        var authOrchestrator = new AcrAuthOrchestrator(_authHandler, _pipeline, _publisher, _jsonOptions);
        _authHandler.RequestAuthProvider = authOrchestrator;
        string refreshToken = await authOrchestrator.AcquireAcrRefreshTokenAsync(loginServer);

        _authHandler.ApplyOverride = true;

        // Invoke docker login
        await DockerLoginAsync(loginServer, refreshToken);

        return new ConnectResult(loginServer, "Login Succeeded");
    }

    private static async Task DockerLoginAsync(string loginServer, string password)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"login {loginServer} -u {AcrDataPlaneProcessorBase.AcrDockerUserName} --password-stdin",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker process. Ensure docker is installed and available in PATH.");

        await process.StandardInput.WriteLineAsync(password);
        process.StandardInput.Close();

        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Docker login to '{loginServer}' failed (exit code {process.ExitCode}): {error}");
        }
    }
}

internal record ConnectResult(string LoginServer, string Status);
