// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.ArgumentParsing;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Common.ParameterValidation;

public static class ValidationHook
{
    public const string AcceptedMarker = "AZCLIPS_VALIDATION_ACCEPTED_EXECUTION_BLOCKED";

    public static void Register() =>
        AzclipsServiceProvider.OverrideRegistration(services =>
            services.AddSingleton<IArgumentParsingService>(provider => new ValidationArgumentParsingService(
                new ArgumentParsingService(
                    provider.GetRequiredService<IArgumentParsingProvider>(),
                    provider.GetRequiredService<IProfileService>()))));

    private sealed class ValidationArgumentParsingService(IArgumentParsingService inner) : IArgumentParsingService
    {
        public IProcessor BindArgumentsToProcessor(IProcessor processor, IDictionary<string, object> args, CommandMetadata command, Profile? profile)
        {
            inner.BindArgumentsToProcessor(processor, args, command, profile);
            throw AzclipsErrorException.UsageError($"{AcceptedMarker}: {command.InvocationName}. No processor was executed.");
        }
    }
}
