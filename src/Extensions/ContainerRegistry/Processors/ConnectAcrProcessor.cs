// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Orchestrators;

namespace Microsoft.Azure.Azclips.Extensions.ContainerRegistry.Processors;

/// <summary>
/// ACR connect (docker login).
/// Flow: AAD token → ACR refresh token → docker login --password-stdin.
/// CLI: az acr connect --name myregistry
/// PS:  Connect-AzContainerRegistry -Name myregistry
/// </summary>
public class ConnectAcrProcessor : AcrDataPlaneProcessorBase
{
    private const string AcrDockerUserName = "00000000-0000-0000-0000-000000000000";

    [ArgumentInfo(Required = true, Description = "The name of the container registry.", SerializedName = "Name")]
    public string Name { get; set; } = "";

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            string loginServer = AcrTokenProvider.ResolveLoginServer(Name);

            var tokenProvider = new AcrTokenProvider(AuthHandler, JsonOptions);
            string refreshToken = await tokenProvider.AcquireAcrRefreshTokenAsync(loginServer);

            await DockerLoginAsync(loginServer, refreshToken);

            await WriteOutput(new { LoginServer = loginServer, Status = "Login Succeeded" });
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    private static async Task DockerLoginAsync(string loginServer, string password)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"login {loginServer} -u {AcrDockerUserName} --password-stdin",
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
