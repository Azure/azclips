// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Shared parser for the <c>KEY=VALUE</c> token lists the compound arguments use on both surfaces
/// (<c>--tags</c>, <c>--ip-tags</c>, <c>--subnets</c>); the PowerShell cmdlets flatten hashtables
/// into the same tokens before invoking.
/// </summary>
internal static class KeyValueTokens
{
    internal static IEnumerable<KeyValuePair<string, string>> Parse(IEnumerable<string> tokens, string argument)
    {
        foreach (var token in tokens)
        {
            var separator = token.IndexOf('=');
            if (separator <= 0)
            {
                throw new ArgumentException($"'{token}' is not a valid {argument} value; expected KEY=VALUE.");
            }
            yield return new KeyValuePair<string, string>(token[..separator].Trim(), token[(separator + 1)..].Trim());
        }
    }
}
