// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime;

/// <summary>Creates independent authenticated pipelines using the invocation's runtime configuration.</summary>
public interface IHttpPipelineFactory
{
    IRuntimeHttpPipeline Create(Profile profile, string[] scopes);

    IRuntimeHttpPipeline Create(IProfileCredential credential, string[] scopes);
}
