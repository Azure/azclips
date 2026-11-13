// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Describes immutable metadata supplied by a client when it initializes Core services.
/// </summary>
/// <param name="ClientType">The client surface hosting the Core services.</param>
/// <param name="FirstPartyApplicationId">
/// The Microsoft Entra application ID used for first-party public client authentication.
/// </param>
/// <param name="ReleaseVersion">The client-wide release version, when supplied by the host.</param>
public sealed record ClientMetadata(
    ClientType ClientType,
    string FirstPartyApplicationId,
    string? ReleaseVersion = null);
