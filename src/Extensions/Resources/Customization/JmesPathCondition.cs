// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Evaluates a <c>--custom</c> JMESPath condition against a resource's JSON the way Azure CLI's
/// <c>wait</c> does: the condition holds when the result is truthy in Python terms, so <c>null</c>,
/// <c>false</c>, <c>0</c>, <c>""</c>, <c>[]</c>, and <c>{}</c> don't count.
/// </summary>
/// <remarks>
/// JmesPath.Net is only referenced here, so it's loaded only when a command passes <c>--custom</c>.
/// The PowerShell module doesn't ship it, which is fine because no cmdlet takes a custom condition.
/// </remarks>
internal static class JmesPathCondition
{
    private static readonly DevLab.JmesPath.JmesPath s_jmesPath = new();

    // JmesPath.Net reports syntax and evaluation errors as plain System.Exception.
    internal static void Validate(string expression)
    {
        try
        {
            _ = s_jmesPath.Parse(expression);
        }
        catch (Exception ex)
        {
            throw AzclipsErrorException.InvalidInput($"--custom expression '{expression}' is not valid JMESPath: {ex.Message}", innerException: ex);
        }
    }

    internal static bool IsMet(string json, string expression)
    {
        string result;
        try
        {
            result = s_jmesPath.Transform(json, expression);
        }
        catch (Exception ex)
        {
            throw AzclipsErrorException.InvalidInput($"--custom expression '{expression}' could not be applied: {ex.Message}", innerException: ex);
        }

        using JsonDocument document = JsonDocument.Parse(result);
        JsonElement value = document.RootElement;
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined or JsonValueKind.False => false,
            JsonValueKind.String => value.GetString()!.Length > 0,
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            JsonValueKind.Number => value.GetDouble() != 0,
            _ => true,
        };
    }
}
