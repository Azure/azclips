// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Shared parser for the <c>KEY=VALUE</c> token lists the compound arguments use on both surfaces
/// (<c>--tags</c>, <c>--ip-tags</c>, <c>--subnets</c>); the PowerShell cmdlets flatten hashtables
/// into the same tokens before invoking. <paramref name="allowBareKey"/> follows Azure CLI's
/// <c>KEY[=VALUE]</c> tag syntax, where a bare <c>KEY</c> carries an empty value.
/// </summary>
internal static class KeyValueTokens
{
    internal static IEnumerable<KeyValuePair<string, string>> Parse(IEnumerable<string> tokens, string argument, bool allowBareKey = false)
    {
        foreach (var token in tokens)
        {
            var separator = token.IndexOf('=');
            if (separator < 0 && allowBareKey && !string.IsNullOrWhiteSpace(token))
            {
                yield return new KeyValuePair<string, string>(token.Trim(), string.Empty);
                continue;
            }
            if (separator <= 0)
            {
                throw new ArgumentException($"'{token}' is not a valid {argument} value; expected {(allowBareKey ? "KEY[=VALUE]" : "KEY=VALUE")}.");
            }
            yield return new KeyValuePair<string, string>(token[..separator].Trim(), token[(separator + 1)..].Trim());
        }
    }
}
