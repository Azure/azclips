// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Azclips.Help.Tool.Validation
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
        // CLI docs must include at least one ```bash/```sh fence so ExampleSyntaxValidator can scan it.
        public const string MD_NoCliExamples = "MD_NoCliExamples";

        // Stub-content rules. Fire from MarkdownStructureValidator on every CLI markdown file.
        public const string MD_TodoPlaceholder = "MD_TodoPlaceholder";
        public const string CLI_ExamplePlaceholder = "CLI_ExamplePlaceholder";

        // MarkdigWellFormednessValidator. Fires on prose (non-fenced-code content) that
        // would render as surprising HTML through docfx's Markdig pipeline.
        public const string MD_SuspiciousHtmlTag = "MD_SuspiciousHtmlTag";
        public const string MD_UnbalancedBackticks = "MD_UnbalancedBackticks";

        // HelpCompletenessValidator
        public const string Completeness_MissingHelpFile = "Completeness_MissingHelpFile";
        public const string Completeness_OrphanedHelpFile = "Completeness_OrphanedHelpFile";
        // `index` is reserved: it names group-summary files at every level of the nested corpus.
        // Routed commands containing an `index` segment would collide with those files.
        public const string Completeness_ReservedSegment = "Completeness_ReservedSegment";

        // ExampleSyntaxValidator (CLI)
        public const string CLI_L1_UnknownGroup = "CLI_L1_UnknownGroup";
        public const string CLI_L1_UnknownCommand = "CLI_L1_UnknownCommand";
        public const string CLI_L2_UnknownFlag = "CLI_L2_UnknownFlag";
        public const string CLI_L3_InvalidValue = "CLI_L3_InvalidValue";
        // Every CLI example must include all required parameters on the routed command (mirrors PSL3).
        public const string CLI_L4_MissingRequiredFlag = "CLI_L4_MissingRequiredFlag";

        // PowerShellExampleSyntaxValidator
        public const string PS_ParseError = "PS_ParseError";
        public const string PSL1_UnknownCmdlet = "PSL1_UnknownCmdlet";
        public const string PSL2_UnknownParameter = "PSL2_UnknownParameter";
        public const string PSL3_MissingRequiredParameter = "PSL3_MissingRequiredParameter";
        public const string PSL4_DuplicateParameter = "PSL4_DuplicateParameter";
        public const string PSL5_UnassignedVariable = "PSL5_UnassignedVariable";
        public const string PSL6_InvalidParameterValue = "PSL6_InvalidParameterValue";
        public const string PSL7_ExampleMustInvokeDocumentedCmdlet = "PSL7_ExampleMustInvokeDocumentedCmdlet";
        public const string PSL8_NoExamples = "PSL8_NoExamples";
        // PS stub-content rules. Distinct IDs from CLI counterparts for log greppability.
        public const string PS_TodoPlaceholder = "PS_TodoPlaceholder";
        public const string PS_ExamplePlaceholder = "PS_ExamplePlaceholder";

        // PsCompletenessValidator: every cmdlet in <Module>.psd1's CmdletsToExport
        // must have a matching src/PS/<Module>/help/<Verb-Noun>.md.
        public const string PSL9_MissingHelpFile = "PSL9_MissingHelpFile";
        public const string PSL10_MissingHelpFolder = "PSL10_MissingHelpFolder";
        public const string PSL11_OrphanedHelpFile = "PSL11_OrphanedHelpFile";

        // PlatyPS `{{ Fill ... }}` scaffold markers. Symmetric with PS_TodoPlaceholder.
        public const string PSL12_FillPlaceholder = "PSL12_FillPlaceholder";

        // PlatyPS refresh-and-diff (build/Test-PSHelpMarkdown.ps1). Emitted by that script, not by
        // this C# tool; declared here so PSL* IDs share one grep-able registry.
        public const string PSL13_SchemaFailure = "PSL13_SchemaFailure";
        public const string PSL14_ParameterDrift = "PSL14_ParameterDrift";
        public const string PSL15_MetadataDrift = "PSL15_MetadataDrift";

        public const string PSL16_EmptyLinkTarget = "PSL16_EmptyLinkTarget";
        public const string PSL17_InvalidDate = "PSL17_InvalidDate";

        // Tool-level (Program.cs)
        public const string Tool_CmdletAssemblyLoadFailed = "Tool_CmdletAssemblyLoadFailed";
        public const string ExtensionIndexLoadFailed = "Tool_ExtensionIndexLoadFailed";

        // ParameterGroupConventionValidator. Warns when a `[ParameterGroup]` value doesn't end in
        // " Arguments" (the convention Azure CLI uses uniformly for its group headers).
        public const string GRP_NonConventionalGroupName = "GRP_NonConventionalGroupName";
    }
}
