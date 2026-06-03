// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Cirrus.Help.Tool.Validation
{
    /// <summary>
    /// Stable identifiers for every rule a help validator can fire. The string values
    /// are part of the tool's contract: the build-time summary groups by these IDs and
    /// CI will eventually baseline per rule. Add a new constant when adding a rule;
    /// renaming an existing one is a breaking change for any consumer that filters logs.
    /// </summary>
    public static class ProblemIds
    {
        // MarkdownStructureValidator
        public const string MD_ExtensionsDirMissing = "MD_ExtensionsDirMissing";
        public const string MD_NoMarkdownFiles = "MD_NoMarkdownFiles";
        public const string MD_UnbalancedFences = "MD_UnbalancedFences";
        public const string MD_MissingFrontmatter = "MD_MissingFrontmatter";
        public const string MD_MissingSummary = "MD_MissingSummary";
        public const string MD_MissingSchema = "MD_MissingSchema";
        public const string MD_InvalidStatus = "MD_InvalidStatus";
        public const string MD_MissingDescription = "MD_MissingDescription";
        public const string MD_NoExamples = "MD_NoExamples";

        // HelpCompletenessValidator
        public const string Completeness_MissingHelpFile = "Completeness_MissingHelpFile";
        public const string Completeness_OrphanedHelpFile = "Completeness_OrphanedHelpFile";

        // ExampleSyntaxValidator (CLI)
        public const string CLI_L1_UnknownGroup = "CLI_L1_UnknownGroup";
        public const string CLI_L1_UnknownCommand = "CLI_L1_UnknownCommand";
        public const string CLI_L2_UnknownFlag = "CLI_L2_UnknownFlag";
        public const string CLI_L3_InvalidValue = "CLI_L3_InvalidValue";

        // PowerShellExampleSyntaxValidator
        public const string PS_ParseError = "PS_ParseError";
        public const string PSL1_UnknownCmdlet = "PSL1_UnknownCmdlet";
        public const string PSL2_UnknownParameter = "PSL2_UnknownParameter";
        public const string PSL3_MissingRequiredParameter = "PSL3_MissingRequiredParameter";
        public const string PSL4_DuplicateParameter = "PSL4_DuplicateParameter";
        public const string PSL5_UnassignedVariable = "PSL5_UnassignedVariable";
        public const string PSL6_InvalidParameterValue = "PSL6_InvalidParameterValue";
        public const string PSL7_ExampleMustInvokeDocumentedCmdlet = "PSL7_ExampleMustInvokeDocumentedCmdlet";

        // Tool-level (Program.cs)
        public const string Tool_CmdletAssemblyLoadFailed = "Tool_CmdletAssemblyLoadFailed";
    }
}
