// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Benchmark;

/// <summary>
/// Tags a benchmark class with the Azclips extension it exercises (e.g. "Resources",
/// "Compute"). Used by <see cref="Program"/> to discover and filter benchmarks by
/// extension. Every concrete benchmark class MUST carry this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class BenchmarkExtensionAttribute : Attribute
{
    public string Name { get; }

    public BenchmarkExtensionAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Extension name is required.", nameof(name));
        }
        Name = name;
    }
}
