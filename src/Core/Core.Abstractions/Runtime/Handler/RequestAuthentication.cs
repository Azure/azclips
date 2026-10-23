// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions.Runtime.Handler;

/// <summary>Per-request authentication without changing a shared handler's profile or override.</summary>
public sealed record RequestAuthentication(Profile Profile, string[] Scopes)
{
    public static readonly HttpRequestOptionsKey<RequestAuthentication> OptionsKey = new("Azclips.RequestAuthentication");
}
