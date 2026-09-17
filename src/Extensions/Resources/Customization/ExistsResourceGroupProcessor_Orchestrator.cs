// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Returns whether a resource group exists.</summary>
public class ExistsResourceGroupProcessor_Orchestrator : CheckExistenceResourceGroupProcessor
{
    public override async Task ProcessAsync()
    {
        try
        {
            await BeforeProcessAsync();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            using HttpRequestMessage request = await CreateRequestMessage();
            using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
            await Publisher.Publish(Events.TracingHttpResponseMessage, response);

            bool exists = response.StatusCode != HttpStatusCode.NotFound;
            if (exists)
            {
                await response.EnsureSuccessAsync(Publisher);
            }

            await WriteOutput(exists);
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
