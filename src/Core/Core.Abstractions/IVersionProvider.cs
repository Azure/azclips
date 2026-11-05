// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public interface IVersionProvider
{
    string ProductVersion { get; }
}
