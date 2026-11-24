---
title: Declarative create tooling research
description: Source-backed inventory of Azure tooling and libraries for reviewing the declarative create engine prototype.
---

# Declarative create tooling research

**Research date:** 2026-11-24

**Status:** Research findings and evaluation candidates, not an approved design or dependency decision.

## Context

The reviewed [POC][poc] and [design review][design-review] are pinned to commit
`05c2172b046d3ccc94dcf3c9ae1838ff567da2b4` in the historical
`YangAn-microsoft/cirrus` fork. The continuation branch is
[`feat/declarative-create-poc-v2`][poc-branch].

That snapshot describes a create-only prototype demonstrated for VM, Public IP, and Resource
Group create. Extensions contribute resource descriptors and command JSON assets. A shared
engine resolves create-versus-reuse, synthesizes an ARM template, submits a deployment, polls,
and retrieves the root resource. Resource schemas are consumed at build time, not shipped
in the runtime. Updates/PATCH are outside that snapshot's intended scope.

This reference records a survey of relevant public tools, APIs, libraries, and frameworks.
It is not an audit of the current product implementation or a claim that the prototype has
been merged. Package versions and support statements below are dated observations and must
be checked again before adoption. No integration tests or performance benchmarks were run.

## Executive findings

1. The shared declarative engine is a reasonable candidate, but should be compared with
   **build-time Bicep compilation**, not only runtime Bicep compilation.
2. Existing Bicep type loaders may replace the handwritten type-index parser while keeping
   schema processing entirely in build tooling.
3. A dedicated deployment SDK and a standalone resource-validation API offer opportunities
   to reduce custom transport and validation code.
4. Bicep's documented JSON-RPC protocol is stable, but its .NET client package and compiler
   core explicitly disclaim support. Public availability is not a support guarantee.
5. ARM deployments provide orchestration, preflight, what-if, and diagnostics, but are not
   atomic transactions. Direct resource requests also receive Azure Policy enforcement.
6. No surveyed tool automatically supplies command aliases, input derivation,
   create-versus-reuse policy, and CLI/PowerShell compatibility together.

## 1. Template generation and compilation

| Tool or library | Capability | Assessment for this design |
| --- | --- | --- |
| [Bicep CLI][bicep-cli] | ARM JSON compilation, parameter compilation, module restore, linting, formatting, and decompilation. | Strong build-time candidate. A compiler dependency at build time does not require shipping a compiler at runtime. |
| [Bicep JSON-RPC][bicep-rpc] | Structured compilation diagnostics, parameter/output metadata, deployment graphs, file references, and snapshots; supports a reusable compiler process. | Important documented integration boundary. The protocol is described as stable and backward-compatible. |
| [Azure.Bicep.RpcClient][rpc-client] | .NET wrapper for installing and communicating with Bicep CLI. | Convenient, but the package explicitly disclaims support and permits breaking changes. Assess separately from the protocol contract. |
| [Azure.Bicep.Core][bicep-core] | In-process compiler functionality. | Public but explicitly unsupported. Do not make it the default runtime foundation without accepting and isolating that risk. |
| [Azure.Provisioning][provisioning] and service packages | Typed .NET infrastructure objects, references, expressions, and Bicep generation. | Worth comparing; emits Bicep rather than directly ARM JSON. Does not automatically generate our command ergonomics. |
| [Azure.Deployments.Templates][deployment-templates] and [Azure.Deployments.Expression][deployment-expression] | ARM template parsing and expression machinery. | Potential tooling/test dependencies; both explicitly disclaim support. Not equivalent to a supported embedded ARM execution engine. |
| [Farmer][farmer] | Community F#/.NET library generating ARM templates directly. | Useful comparator for a lightweight emitter. Resource coverage, API-version coverage, and maintenance need evaluation. |

### Version and support observations

- [Azure.Provisioning][provisioning-package] has a stable `1.6.0` package.
- [Azure.Provisioning.Compute][provisioning-compute] is `1.0.0-beta.3`, a prerelease.
  Assess each service package individually rather than classifying the whole family as stable
  or prerelease.
- Bicep JSON-RPC was introduced in CLI `0.29.45`; individual newer methods have their own
  minimum versions. Pin the CLI and verify the methods actually used.
- The [RpcClient project][rpc-client] targets .NET Standard 2.0, but compatible target
  frameworks do not establish package support or prove compatibility with our hosting model.

### Build-time Bicep deserves a separate comparison

The prototype's alternative-design discussion focuses on generating Bicep and compiling it
for each invocation. A different option is to author parameterized Bicep assets, compile them
during the build, and ship the resulting ARM JSON.

Compare that option with the custom descriptor/body IR and emitter. It may reduce compiler
maintenance without adding runtime compilation, but must still support the required resource
graph variations, API versions, input mapping, and create-versus-reuse decisions.

Bicep's [existing resource declarations][existing-resource] reference resources without
redeploying them. They do not automatically implement "GET by name, create on 404"; that
command policy still needs an explicit design.

## 2. Resource schemas and code generation

| Tool or source | Capability | Assessment |
| --- | --- | --- |
| [bicep-types-az][bicep-types-az] | Generated resource definitions indexed by resource type and API version. | Strong build-time source. Pin provenance and verify required coverage; do not assume every service/version is complete. |
| [Azure.Bicep.Types][type-loader] | Existing deserializer, type loader, and concrete type model. | Evaluate replacing the handwritten parser. Keep the dependency in build tooling if runtime schema processing is unnecessary. |
| [Azure.Bicep.Types.Az][az-type-loader] | Packaged compressed Azure type catalogue and loader. | Alternative to individually vendored files. Catalogue size is less concerning when it is not shipped at runtime. |
| [azure-rest-api-specs][rest-specs], [TypeSpec ARM][typespec-arm], and [TCGC][tcgc] | API contracts, operation shapes, versioning, and code-generation models. | Useful for descriptor skeletons and distinguishing create/update/action contracts. They do not infer command-specific defaults or reuse policy. |
| [ARM JSON schemas][arm-schemas] | Template authoring schemas and IntelliSense. | Best-effort authoring aid, not an authoritative programmatic foundation. The repository explicitly warns against relying on accuracy or programmatic consumption. |
| [JsonSchema.Net][json-schema] | Standard JSON Schema validation with structured error locations. | Candidate for validating our command/descriptor asset format, not a replacement for ARM resource semantics. |

**Keep the boundaries explicit:**

- Resource schema describes legal resource data.
- Command metadata describes the user experience.
- Command policy determines defaults, dependency fulfillment, and reuse.
- ARM remains the server-side authority.

A build-time schema pipeline should have a clear provenance and API-version contract.
An `--api-version` override must select a compatible, validated descriptor/schema variant;
changing only the version string does not establish body compatibility.

## 3. Deployment and validation APIs

| API or library | Opportunity | Caveat |
| --- | --- | --- |
| [Azure.ResourceManager.Resources.Deployments][deployment-sdk] | Dedicated stable deployment SDK; compare with handwritten HTTP transport and LRO handling. | Preserve AzCLIPS authentication, events, cancellation, logging, and error behavior. Confirm required API features before replacing transport. |
| [Deployment validate][deployment-validate] | Validate the complete generated deployment. | Handle asynchronous responses as well as immediate results. Validation does not guarantee later deployment success. |
| [Validation levels][validation-levels] | Explicit `Provider`, `ProviderNoRbac`, and `Template` choices. | These have different checks and permission requirements; do not silently treat them as interchangeable. |
| [Standalone resource validator][resource-validator] | Validate resource PUT bodies without wrapping them in a deployment template. | Batch requests group resources by provider/type/location. Verify supported checks and service coverage. |
| [Deployment operations][deployment-operations] | Resource-level progress, target-resource information, and failure details. | Terminal deployment state alone is insufficient for good diagnostics. |
| [Azure.Core.ResourceIdentifier][resource-identifier] | Resource-ID parsing and hierarchy handling. | Still enforce expected resource type, scope, and command-specific constraints. Parsing alone is not an authorization check. |
| [Provider metadata][provider-metadata] | Supported API versions, locations, and registration state. | Not a complete resource-body schema. |

### Standalone validation is a significant additional option

The documented endpoint is:

```http
POST https://management.azure.com/providers/Microsoft.Resources/validateResources?api-version=2022-06-01
```

Its request contains the provider, type, scope, optional location, and resource bodies with
their API versions. It supports `ArmFull` and `ArmPartial`, and an option to skip RBAC write
checks. The request model also exposes `onlyIfNotExists`; this is a validation input, not
a guarantee of conditional creation during a later deployment.

[AzAPI preflight][azapi-preflight] is useful implementation prior art. Its documentation
identifies limits: unsupported resource types may be skipped, and cross-resource sequencing
is not validated. Do not adopt silent skips as a success-shaped result in AzCLIPS.

This API is especially relevant to future direct resource operations: deploying a template
is not the only way to obtain server-side preflight.

### Validation-level semantics

- `Provider`: full validation, including deployment permission checks.
- `ProviderNoRbac`: provider validation with read-permission checks instead of full
  deployment permissions.
- `Template`: static validation, skipping provider preflight and deployment permission checks.

[Preflight][preflight] is best effort. Runtime failures, unresolved dependencies, and some
permission-inheritance scenarios can still require special handling.

## 4. Reusable modules and Portal tooling

| Tool or framework | Relevance | Boundary |
| --- | --- | --- |
| [Azure Verified Modules][avm] | Reusable resource and architecture modules. | Consider optional compositions. Pin versions and verify that module defaults preserve command behavior. |
| [Bicep registries/module restore][bicep-restore] | Versioned module distribution and caching. | Build-time restore avoids runtime registry dependencies; use reproducible inputs and an explicit offline story. |
| [Template Specs][template-specs] | Versioned, RBAC-controlled template distribution shared across Portal, CLI, and PowerShell. | Optional publishing mechanism, not required for every command invocation. Nested template links can limit what-if visibility. |
| [Portal Form view / uiFormDefinition][form-view] | Public JSON-defined forms, validation controls, and deployment-context bindings. | Strong prior art for separating command-surface metadata from template inputs. |
| [CreateUiDefinition][create-ui] | Declarative UI model for Managed Applications. | Distinct from Form view; the formats are not interchangeable. |
| [Azure.ResourceManager.Resources.Bicep][bicep-sdk] | Inspected public API exposes ARM-to-Bicep decompilation. | Despite the package name, it is not a replacement local compiler. Potentially useful for human-readable exports. |

Portal forms support the general "surface metadata plus template parameters" model.
They are not a universal reusable library implementing every Portal resource-creation
workflow. Avoid treating all Portal behavior as proof of a particular create/update strategy.

## 5. Testing and command-tooling prior art

| Tool | Best use | Limitation |
| --- | --- | --- |
| [Bicep snapshots][snapshots] | Local normalized infrastructure comparisons for tested inputs; useful for Bicep authoring and refactoring. | Not live-state validation, proof of deployment success, or proof of equivalence for all inputs. |
| [PSRule for Azure][psrule] | Best-practice checks on generated infrastructure. | Complementary to schema validation and server preflight; select rules appropriate to the command contract. |
| [ARM TTK][arm-ttk] | JSON-template and CreateUiDefinition checks. | Maintainers are moving new deployment-template test investment toward the Bicep linter. |
| [Azure CLI VM template builder][cli-template-builder] | Concrete prior art for VM/network synthesis and dependencies. | Compatibility reference, not a drop-in .NET library. |
| [AAZ tooling][aaz] and [AutoRest.PowerShell][autorest-ps] | Command-model generation and customization patterns. | Do not themselves solve ARM resource-graph synthesis. |
| [Azure Deployment Action][deployment-action] | Deployment, validate, what-if, stack automation, and examples. | CI/reference tooling, not an embedded command runtime. |
| [AzAPI][azapi-preflight] and [Pulumi Azure Native][pulumi] | Comparators for generic resource bodies, API-version handling, and preflight. | Adopting their full execution/state engines would substantially expand scope. |
| [Aspire][aspire] and [Azure Developer CLI][azd] | App-centric infrastructure generation and deployment patterns. | Not direct replacements for a general Azure command runtime. |

Snapshots are documented as generally available with Bicep CLI `0.41.2` or later; the Azure
CLI wrapper requires `2.90.0` or later. Local comparison does not require Azure sign-in,
although restoring external modules can require network access and authentication.

For direct JSON synthesis, retain focused emitted-template tests and server validation.
Do not assume Bicep snapshot tooling is a drop-in validator for arbitrary custom JSON assets.

## 6. Safety and semantic corrections

### Deployments are not atomic transactions

A single deployment can fail after other resources have successfully deployed.
Microsoft's [troubleshooting example][partial-failure] explicitly demonstrates this.
Do not promise automatic rollback, absence of partial resources, or race-free creation.

GET-before-create also leaves a time-of-check/time-of-use window. A resource included in an
ARM template can be updated if it exists by deployment time. Define whether "create-only"
means the command family being modeled or a strict no-overwrite guarantee.

### Direct resource requests also receive policy enforcement

[Azure Policy deny evaluation][policy-deny] applies to resource create/update requests before
they reach the resource provider. Deployment-specific benefits are orchestration, explicit
preflight, what-if, and shared diagnostics, not exclusive policy or permission enforcement.

### Incremental mode is not PATCH

[Incremental deployment mode][deployment-modes] leaves resources omitted from the template
unchanged, but reapplies the properties of resources included in it. Omitted properties on
those resources can reset to defaults. This supports keeping partial updates separate, but
does not imply ARM deployments cannot perform any updates.

### An incomplete preview must remain visibly incomplete

[What-if limitations][what-if] include unresolved expressions, short-circuiting, template
links, and expansion limits. Resources can be omitted or returned as `Ignore`.
A successful HTTP response or an empty change list is not proof of a complete preview.
Preserve diagnostic messages and distinguish "no changes" from "not fully analyzed."

### Lifecycle management is a separate decision

[Deployment stacks][deployment-stacks] provide ownership, detach/delete behavior, and deny
settings. They should not silently become the backing mechanism for ordinary create
commands. Microsoft recommends stacks rather than Complete mode for template-driven
deletions; that does not justify expanding the create engine's contract.

## 7. Recommended evaluation order

1. **Clarify command semantics first:** root-resource reuse, dependency reuse, handling of
   existing resources, API-version selection, partial failures, and scope behavior.
2. **Compare three synthesis approaches:** custom JSON/body IR, parameterized build-time
   Bicep assets, and Azure.Provisioning.
3. **Evaluate existing type loaders:** replace custom parsing only if the required schema
   features, diagnostics, and version-pinning contract are preserved.
4. **Evaluate deployment SDK and standalone validation:** preserve the framework's existing
   behavior instead of bypassing its authentication, events, or diagnostics.
5. **Add validation and preview guardrails:** explicit levels, incomplete-result reporting,
   resource-level failures, and secret-safe exports.
6. **Keep optional integrations optional:** AVM, Portal forms, Template Specs, and deployment
   stacks should have separately justified contracts.

### Comparison evidence to collect

Run the same VM, Public IP, and Resource Group scenarios through each viable approach:

| Dimension | Evidence required |
| --- | --- |
| Output semantics | Resource types, names, scopes, API versions, non-default properties, dependencies, parameters, and secret references match the intended graph. |
| Reuse | Existing dependency, missing dependency, bring-your-own ID, existing root resource, and resource-appearance race behavior are explicit. |
| Validation | Missing/invalid inputs, enum and primitive constraints, unsupported API versions, scope errors, and partial preview diagnostics fail or warn consistently. |
| Version changes | Show what must change when adopting a new resource API version; do not just substitute a version string. |
| Diagnostics | Preserve cancellation, nested ARM errors, resource progress, deployment identity, and correlation information. |
| Packaging | Record shipped dependency and asset sizes, network/restore requirements, and host/extension loading behavior. |
| Performance | Measure cold and warm latency separately; distinguish compilation/binding time from live API and deployment time. |
| Maintenance | Compare resource onboarding, asset/code changes, schema upgrades, and the amount of compiler logic we own. |

No approach is selected by this reference. Performance or coverage claims require measured
results; passing snapshots establish only the tested input/context coverage.

<!-- link references -->

[poc]: https://github.com/YangAn-microsoft/cirrus/blob/05c2172b046d3ccc94dcf3c9ae1838ff567da2b4/docs/spec/declarative-create-poc.md
[design-review]: https://github.com/YangAn-microsoft/cirrus/blob/05c2172b046d3ccc94dcf3c9ae1838ff567da2b4/docs/spec/declarative-create-design-review.md
[poc-branch]: https://github.com/YangAn-microsoft/cirrus/tree/feat/declarative-create-poc-v2
[bicep-cli]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/bicep-cli
[bicep-rpc]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/bicep-cli-jsonrpc
[rpc-client]: https://github.com/Azure/bicep/blob/main/src/Bicep.RpcClient/Bicep.RpcClient.csproj
[bicep-core]: https://github.com/Azure/bicep/blob/main/src/Bicep.Core/Bicep.Core.csproj
[provisioning]: https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/provisioning/Azure.Provisioning
[provisioning-package]: https://www.nuget.org/packages/Azure.Provisioning/1.6.0
[provisioning-compute]: https://www.nuget.org/packages/Azure.Provisioning.Compute/1.0.0-beta.3
[deployment-templates]: https://www.nuget.org/packages/Azure.Deployments.Templates
[deployment-expression]: https://www.nuget.org/packages/Azure.Deployments.Expression
[farmer]: https://compositionalit.github.io/farmer/deployment-guidance/
[existing-resource]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/existing-resource
[bicep-types-az]: https://github.com/Azure/bicep-types-az
[type-loader]: https://github.com/Azure/bicep-types/blob/main/src/Bicep.Types/TypeLoader.cs
[az-type-loader]: https://github.com/Azure/bicep-types-az/blob/main/src/Bicep.Types.Az/AzTypeLoader.cs
[rest-specs]: https://github.com/Azure/azure-rest-api-specs
[typespec-arm]: https://github.com/Azure/typespec-azure/tree/main/packages/typespec-azure-resource-manager
[tcgc]: https://github.com/Azure/typespec-azure/tree/main/packages/typespec-client-generator-core
[arm-schemas]: https://github.com/Azure/azure-resource-manager-schemas
[json-schema]: https://docs.json-everything.net/schema/basics/
[deployment-sdk]: https://www.nuget.org/packages/Azure.ResourceManager.Resources.Deployments/1.0.0
[deployment-validate]: https://learn.microsoft.com/en-us/rest/api/resources/deployments/validate?view=rest-resources-2025-04-01
[validation-levels]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-what-if#what-if-commands
[resource-validator]: https://learn.microsoft.com/en-us/rest/api/resources/resource-validator/validate-resources?view=rest-resources-2022-06-01
[deployment-operations]: https://learn.microsoft.com/en-us/rest/api/resources/deployment-operations/list?view=rest-resources-2025-04-01
[resource-identifier]: https://learn.microsoft.com/en-us/dotnet/api/azure.core.resourceidentifier
[provider-metadata]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-providers-and-types
[azapi-preflight]: https://learn.microsoft.com/en-us/azure/developer/terraform/how-to-use-azapi-preflight-validation
[preflight]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-preflight
[avm]: https://azure.github.io/Azure-Verified-Modules/
[bicep-restore]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/bicep-cli#restore
[template-specs]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/template-specs
[form-view]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/form-view-overview
[create-ui]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/managed-applications/create-uidefinition-overview
[bicep-sdk]: https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/resources/Azure.ResourceManager.Resources.Bicep
[snapshots]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/snapshots
[psrule]: https://azure.github.io/PSRule.Rules.Azure/
[arm-ttk]: https://github.com/Azure/arm-ttk
[cli-template-builder]: https://github.com/Azure/azure-cli/blob/dev/src/azure-cli/azure/cli/command_modules/vm/_template_builder.py
[aaz]: https://github.com/Azure/aaz-dev-tools
[autorest-ps]: https://github.com/Azure/autorest.powershell
[deployment-action]: https://github.com/Azure/bicep-deploy
[pulumi]: https://www.pulumi.com/registry/packages/azure-native/guides/version-guide/
[aspire]: https://aspire.dev/deployment/azure/
[azd]: https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/overview
[partial-failure]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/troubleshooting/quickstart-troubleshoot-arm-deployment
[policy-deny]: https://learn.microsoft.com/en-us/azure/governance/policy/concepts/effect-deny
[deployment-modes]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/templates/deployment-modes
[what-if]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-what-if#limitations
[deployment-stacks]: https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deployment-stacks
