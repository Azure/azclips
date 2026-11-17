// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.Extensions.Accounts.ParameterSets;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Processors
{
    public class LoginProcessorBase : IProcessor
    {
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventPublisher Publisher = null!;
        protected readonly JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;

        /// <summary>The default cloud environment name stored on profiles created by login.</summary>
        protected const string DefaultCloudName = "AzureCloud";

        /// <summary>
        /// Global <c>--profile</c>/<c>-Profile</c> parameter. On login it names an existing profile to
        /// re-authenticate (see login-profile-integration). Bound from the invocation; resolution of the
        /// active profile happens in the Engine.
        /// </summary>
        [ArgumentInfo(
        Required = false,
        Description = @"Re-authenticate the named existing profile (mutually exclusive with --out-profile).",
        SerializedName = @"Profile")]
        public string? Profile { get; set; }

        protected virtual void BeforeInit() { }
        protected virtual void AfterInit() { }

        protected CancellationToken CancellationToken => Publisher.Token;

        public Action Cancel => throw new NotImplementedException();

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private protected ParameterSetResolver<LoginProcessorBase> Resolver { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        public IProcessor Init(ProcessorContext context)
        {
            BeforeInit();

            HttpPipeline = context.HttpPipeline;
            Publisher = context.Publisher;
            WriteOutput = context.OutputHandler.WriteOutput;

            ConfigureParameterSets();

            AfterInit();
            return this;
        }

        protected void ConfigureParameterSets()
        {
            Resolver = new ParameterSetResolver<LoginProcessorBase>();
            DefineParameterSets();
            Resolver.Validate();
        }

        protected virtual void DefineParameterSets() { }

        public virtual Task ProcessAsync() => Task.CompletedTask;
        protected virtual HttpRequestMessage CreateRequestMessage() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");
    }
}