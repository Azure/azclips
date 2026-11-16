// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Language;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

public sealed class PSVersionProvider : IProductVersionProvider
{
    public string ProductVersion { get; } = ReadModuleVersion();

    private static string ReadModuleVersion()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(ModuleInitializer).Assembly.Location)!;
        // Packaged modules put the binary in bin; local builds copy the manifest beside it.
        var manifestPath = Path.GetFileName(assemblyDirectory).Equals("bin", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(assemblyDirectory, "..", "Az.Accounts.psd1")
            : Path.Combine(assemblyDirectory, "Accounts.psd1");
        var ast = Parser.ParseFile(manifestPath, out _, out var errors);
        if (errors.Length > 0)
        {
            throw new ParseException(errors);
        }

        if (ast.EndBlock.Statements.Count != 1 ||
            ast.EndBlock.Statements[0] is not PipelineAst pipeline ||
            pipeline.PipelineElements.Count != 1 ||
            pipeline.PipelineElements[0] is not CommandExpressionAst { Expression: HashtableAst manifestAst } ||
            manifestAst.SafeGetValue() is not Hashtable manifest ||
            manifest["ModuleVersion"] is not string version ||
            !System.Version.TryParse(version, out var parsedVersion))
        {
            throw new InvalidOperationException(
                $"Accounts module manifest '{manifestPath}' must define a valid ModuleVersion.");
        }

        return parsedVersion.ToString();
    }
}
