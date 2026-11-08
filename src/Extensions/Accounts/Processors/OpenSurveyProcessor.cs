// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Processors;

public class OpenSurveyProcessor : ProfileProcessorBase
{
    internal static ProcessStartInfo CreateStartInfo(IAssemblyLoadingService assemblyLoadingService) =>
        new(AccountsLinks.CreateSurveyUrl(assemblyLoadingService.GetProductVersion()))
    {
        UseShellExecute = true,
    };

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);

        var startInfo = CreateStartInfo(AzclipsServiceProvider.GetRequiredService<IAssemblyLoadingService>());
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not open the system browser to '{startInfo.FileName}'.");

        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }
}
