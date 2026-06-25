// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Login.Processors
{
    public class LogoutProcessor : LoginProcessorBase
    {
        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
            var authService = CirrusServiceProvider.GetRequiredService<IAuthService>();
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            authService.LogoutAsync().GetAwaiter().GetResult();
            authContextService.ClearContextAsync().GetAwaiter().GetResult();
            await WriteOutput(true);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
