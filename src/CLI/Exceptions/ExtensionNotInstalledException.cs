// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Cli.Exceptions;

/// <summary>
/// Thrown by <see cref="Parser.ExtensionDispatcher"/> when the user types a verb that is owned by
/// a known extension but that extension is not installed. Bypasses the AI agent fallback because
/// the intent is unambiguous: the user wants the named extension.
/// </summary>
public sealed class ExtensionNotInstalledException : AzclipsErrorException
{
    public string Verb { get; }
    public string PackageId { get; }

    public ExtensionNotInstalledException(string verb, string packageId)
        : base(
            code: "ExtensionNotFound",
            exitCode: 4,
            message: BuildMessage(verb, packageId),
            details: new Dictionary<string, object?>
            {
                ["verb"] = verb,
                ["packageId"] = packageId,
            })
    {
        Verb = verb;
        PackageId = packageId;
    }

    private static string BuildMessage(string verb, string packageId) =>
        $"Verb '{verb}' is provided by extension '{packageId}', which is not installed.\n" +
        $"Install it with: az extension add --name {Constants.StripKnownExtensionPrefix(packageId)}";
}
