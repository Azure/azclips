// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Login.Processors
{
    public class GetContextProcessor : LoginProcessorBase
    {
        public override async Task ProcessAsync() {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            var authContextService = AzclipsServiceProvider.GetRequiredService<IAuthContextService>();
            AuthContext Context =  authContextService.GetCurrentContextAsync().GetAwaiter().GetResult()!;
            await WriteOutput(Context);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
