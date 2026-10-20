// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Core.Abstractions
{
    public record ParameterMetadata(
        string Name,
        string InvocationName,
        Type ParameterType,
        string Description,
        bool IsRequired,
        string[] Aliases,
        string[] AllowedValues,
        IDictionary<string, ParameterSetReference>? ParameterSets = null);

    public record ParameterSetReference(
        string Name,
        bool IsMandatory = false,
        int Position = -1
    );
}