// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// <see cref="NewSubscriptionDeploymentProcessor"/> that also sends the deployment's top-level <c>tags</c>.
    /// </summary>
    public class TaggedNewSubscriptionDeploymentProcessor : NewSubscriptionDeploymentProcessor
    {
        public IDictionary<string, string>? Tags { get; set; }

        protected override Deployment? CreateRequestBody() => SubscriptionDeploymentTags.Apply(base.CreateRequestBody(), Tags);
    }

    /// <summary>
    /// <see cref="TestSubscriptionDeploymentProcessor"/> that also sends the deployment's top-level <c>tags</c>.
    /// </summary>
    public class TaggedTestSubscriptionDeploymentProcessor : TestSubscriptionDeploymentProcessor
    {
        public IDictionary<string, string>? Tags { get; set; }

        protected override Deployment? CreateRequestBody() => SubscriptionDeploymentTags.Apply(base.CreateRequestBody(), Tags);
    }

    internal static class SubscriptionDeploymentTags
    {
        // No or empty tags leave Tags null, so no 'tags' property is serialized.
        public static Deployment? Apply(Deployment? body, IDictionary<string, string>? tags)
        {
            if (body is not null && tags is { Count: > 0 })
            {
                body.Tags = new Dictionary<string, string>(tags);
            }

            return body;
        }
    }
}
