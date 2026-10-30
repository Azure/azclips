// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime.Handler
{
    /// <summary>
    /// Per-request options honored by the authentication handler.
    /// </summary>
    public static class AuthHandlerOptions
    {
        /// <summary>
        /// When set to true on <see cref="System.Net.Http.HttpRequestMessage.Options"/>, the
        /// authentication handler acquires no token and adds no Authorization header. Use it for
        /// requests to non-ARM hosts (for example, downloading a public template) so the ARM
        /// access token is never sent to them.
        /// </summary>
        public static readonly HttpRequestOptionsKey<bool> SkipAuthorization = new("Azclips.SkipAuthorization");
    }
}
