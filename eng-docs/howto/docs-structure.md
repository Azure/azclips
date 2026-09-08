---
title: Documentation structure for the private preview
description: Learn how to organize conceptual and reference documentation in the AzCLIPS repository for the private preview.
---

# Documentation structure for the private preview

This article describes the documentation structure and contribution guidelines for the private
preview for the end user documentation.

## Documentation structure

Publish all private preview documentation under the [docs][01] folder. Organize content by product
and content type.

```text
docs/
├── azure-cli/
│   ├── docs-conceptual/
│   └── docs-reference/
└── azure-powershell/
    ├── docs-conceptual/
    └── docs-reference/
```

## Place conceptual content

Store conceptual documentation in the appropriate `docs-conceptual` folder for each product.

- Azure CLI conceptual content:
  - [docs/azure-cli/docs-conceptual/][02]

- Azure PowerShell conceptual content:
  - [docs/azure-powershell/docs-conceptual/][03]

All conceptual documentation must be authored in Markdown.

Examples of conceptual content include:

- Getting started
- Installation
- What's new
- AI-based error recovery
- Troubleshooting
- How-To
- Tutorials
- Scenarios

## Place reference documentation

Generate reference documentation with one file per command and store it in the appropriate
`docs-reference` folder.

- Azure CLI:
  - [docs/azure-cli/docs-reference/][04]
  - Markdown and YAML are supported. The current GA documentation uses YAML.

- Azure PowerShell:
  - [docs/azure-powershell/docs-reference/][05]
  - Markdown is required.

## Pull request reviews

For documentation-related pull requests:

- Add [mikefrobbins][06] as a reviewer.
- Add at least one of the following product managers as a reviewer:

  - [Alex-AZPS][07]
  - [aakashbsrj][08]
  - [dcaro][09]

This review process helps ensure technical accuracy and documentation consistency across the
project.

<!-- link references -->

[01]: https://github.com/Azure/azclips/tree/main/docs
[02]: https://github.com/Azure/azclips/tree/main/docs/azure-cli/docs-conceptual
[03]: https://github.com/Azure/azclips/tree/main/docs/azure-powershell/docs-conceptual
[04]: https://github.com/Azure/azclips/tree/main/docs/azure-cli/docs-reference
[05]: https://github.com/Azure/azclips/tree/main/docs/azure-powershell/docs-reference
[06]: https://github.com/mikefrobbins
[07]: https://github.com/Alex-AZPS
[08]: https://github.com/aakashbsrj
[09]: https://github.com/dcaro
