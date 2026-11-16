// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public interface IProductVersionProvider
{
    string ProductVersion { get; }
}
