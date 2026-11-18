// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Maps one input list to a pair of body properties where the service has a singular and a plural
/// form, such as <c>addressPrefix</c> and <c>addressPrefixes</c>.
/// </summary>
internal static class SingularOrPluralMapper
{
    /// <summary>
    /// One item sets the singular property and clears the plural one. More items set the plural
    /// property in input order and clear the singular one. A null or empty list is "not given" and
    /// changes nothing. Clearing the other property makes the result the same for a new body and
    /// for a body read by a GET.
    /// </summary>
    internal static void Apply(IList<string>? values, Action<string?> setSingular, Action<IList<string>?> setPlural)
    {
        if (values is null || values.Count == 0)
        {
            return;
        }

        if (values.Count == 1)
        {
            setSingular(values[0]);
            setPlural(null);
            return;
        }

        setSingular(null);
        setPlural(values.ToList());
    }
}
