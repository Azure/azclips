// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Runtime.Handler;

namespace Core.Runtime;

public sealed class HttpPipelineFactory(
    Func<Profile, IProfileCredential> createCredential,
    Func<SendAsyncStep, IRuntimeHttpPipeline> createPipeline) : IHttpPipelineFactory
{
    public IRuntimeHttpPipeline Create(Profile profile, string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Create(createCredential(profile), scopes);
    }

    public IRuntimeHttpPipeline Create(IProfileCredential credential, string[] scopes)
    {
        var authentication = new CredentialAuthHandler(credential, scopes);
        return createPipeline(authentication.SendAsync);
    }
}
