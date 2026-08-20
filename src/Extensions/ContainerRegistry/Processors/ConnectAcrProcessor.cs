// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Models;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// Validates connectivity and authentication to an Azure Container Registry.
/// Performs docker login by acquiring ACR credentials and invoking the local docker executable.
/// CLI: az acr connect --name myregistry
/// PS:  Connect-AzContainerRegistry -Name myregistry
/// </summary>
public class ConnectAcrProcessor : AcrDataPlaneProcessorBase
{
    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "Name")]
    public string Name { get; set; } = "";

    public string UserName { get; private set; } = AcrDockerUserName;
    public string Password { get; private set; } = "";

    protected override string GetLoginServer() => ResolveLoginServer(Name);

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            string loginServer = GetLoginServer();

            // Acquire ACR refresh token via the auth orchestrator (AAD token → ACR refresh token)
            var authOrchestrator = new AcrAuthOrchestrator(AuthHandler, HttpPipeline, Publisher, JsonOptions);
            AuthHandler.RequestAuthProvider = authOrchestrator;
            string refreshToken = await authOrchestrator.AcquireAcrRefreshTokenAsync(loginServer);

            // Use refresh token as the docker login password
            Password = refreshToken;
            AuthHandler.ApplyOverride = true;

            // Call docker login
            await DockerLoginAsync(loginServer);

            await WriteOutput(new { LoginServer = loginServer, Status = "Login Succeeded" });
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    private async Task DockerLoginAsync(string loginServer)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"login {loginServer} -u {UserName} --password-stdin",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker process.");

        await process.StandardInput.WriteLineAsync(Password);
        process.StandardInput.Close();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Docker login failed (exit code {process.ExitCode}): {error}");
        }
    }
}
