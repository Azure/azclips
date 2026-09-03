// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

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

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor Init(ProcessorContext context)
        {
            BeforeInit();

            HttpPipeline = context.HttpPipeline;
            Publisher = context.Publisher;
            WriteOutput = context.OutputHandler.WriteOutput;

            AfterInit();
            return this;
        }

        public virtual Task ProcessAsync() => Task.CompletedTask;
        protected virtual HttpRequestMessage CreateRequestMessage() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");
    }
}