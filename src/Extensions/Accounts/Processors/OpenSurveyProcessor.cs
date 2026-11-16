// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Processors;

public class OpenSurveyProcessor : ProfileProcessorBase
{
    internal static ProcessStartInfo CreateStartInfo() => new(AccountsLinks.Survey)
    {
        UseShellExecute = true,
    };

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);

        var startInfo = CreateStartInfo();
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not open the system browser to '{startInfo.FileName}'.");

        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }
}
