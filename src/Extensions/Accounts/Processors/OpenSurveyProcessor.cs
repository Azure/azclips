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

        using Process process = Process.Start(CreateStartInfo())
            ?? throw new InvalidOperationException($"Could not open the system browser to '{AccountsLinks.Survey}'.");

        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }
}
