// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Records the <c>az</c> command whose output supplies valid values for the annotated
/// parameter (e.g. <c>"az vm list-sizes"</c> for <c>--size</c>). Rendered on the docs
/// page as "Values from: &lt;command&gt;"; purely informational — the runtime does not
/// execute the populator.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class PopulatorCommandAttribute : Attribute
{
    public PopulatorCommandAttribute(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command must be non-empty.", nameof(command));
        }

        Command = command;
    }

    public string Command { get; }
}
