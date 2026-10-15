---
name: migrate-clips-to-extensions-codegen-llm-hybrid
description: "Migrate legacy Azure CLI and Azure PowerShell commands to AzClips extensions using TypeSpec codegen plus LLM-authored customization. Use when porting existing command groups, comparing legacy command shapes and workflows with REST APIs, selecting generated processors, or composing migration orchestrators."
---

# Migrate Legacy Clips Commands to AzClips Extensions

Migrate existing legacy clips commands to AzClips extensions while following the repository's current processor, registration, and packaging patterns.

**Input**: The request should identify an extension or Azure service and the operations to expose. Useful optional details include TypeSpec URL, API version, command group, command names, and legacy CLI, PowerShell links.

## Workflow

0. **Resolve paths**
	- `<project-root>` is the absolute path to the AzClips repository root containing `azclips.slnx`.
	- Resolve it from the workspace before running commands.
	- Run terminal commands from `<project-root>` unless a step explicitly changes the working directory.

1. **Install Node libraries and build codegen toolkit**
	- skip if present
	- Install nvs (Node Version Switcher) to manage Node.js versions.
	- Install node 24 using nvs: `nvs add 24`
	- Use node 24: `nvs use 24`
	- Locate into codegen toolkit directory: `cd <project-root>/toolkit`
	- Run `npm ci`
	- Run `./build.ps1` to build the codegen toolkit.

2. **Get Azure-PowerShell in local git repository**
	- skip if present
	- Locate into your workspace: `cd <project-root>/../`
	- Clone the Azure PowerShell repository: `git clone https://github.com/Azure/azure-powershell.git`

3. **Get Azure-Cli in local git repository**
	- skip if present
	- Locate into your workspace: `cd <project-root>/../`
	- Clone the Azure CLI repository: `git clone https://github.com/Azure/azure-cli.git`

4. **Create template aceconfig.yaml**
	- Ask user for desired extension name: `<extension-name>`
	- Ask user to provide a temporary output location `<output-path>`, otherwise fallback to: `<output-path> = <project-root>/../tsp-output/<extension-name>`
	- ensure the output directory exists: `mkdir -p <output-path>`
	- Create `aceconfig.yaml` under `<output-path>` following below template
	```
	schema-version: 1

	# Run inputs: the CLI copies the spec; tsp-emit compiles each entrypoint.
	output-dir: .                            # default: this file's folder
	tsp-location: https://github.com/Azure/azure-rest-api-specs/tree/main/specification/compute/resource-manager/Microsoft.Compute/Compute
	# additional-directories:                # folders the compile needs, not under tsp-location
	#   - ../Shared                          # relative to tsp-location, or absolute
	entrypoints:                             # relative to tsp-location
		- path: ./Compute
			api-version: 2026-03-01

	# Document facts: what the spec cannot provide; seed-ir writes them into the IR's target.
	service-name: Compute
	root-namespace: Microsoft.Azure.Azclips.Extensions.Compute
	project-name: Microsoft.Azure.Azclips.Extensions.Compute
	description: Compute extension for AzClips
	```

5. **Generate extensions from selected API using raw codegen toolkit**
	- Replace `service-name` in `aceconfig.yaml` with `<extension-name>` and update `root-namespace`, `project-name`, and `description` accordingly.
	- Ask user to provide spec path or detail for the API they want to generate extensions for, need to specify management plane or data plane explicitly.
	- Locate API spec: 
		- management plane: `<spec-path> = https://github.com/Azure/azure-rest-api-specs/specification/<rp-name>/resource-manager/<rp-full-name>/<service-name>`
		- data plane: `<spec-path> = https://github.com/Azure/azure-rest-api-specs/specification/<rp-name>/data-plane/<service-name>`
	- If typespec not found from `tsp-location` under `aceconfig.yaml`, search `https://github.com/Azure/azure-rest-api-specs/specification/<rp-name>` for typespec, and let user choose if found any. Once confirmed, update `tsp-location` accordingly.
	- Let user decide which API version to use for the extension. Fallback to latest stable version if not specified. Update `aceconfig.yaml` accordingly.
	- `cd <output-path>`
	- generate raw extensions: `node <project-root>/toolkit/packages/cli/dist/src/cli.js compile`
	- confirm raw extensions are generated under `<output-path>`, no need to compile right now.

6. **Select APIs needed for migration**
	- Review the generated raw extensions under `<output-path>`.
	- Ask user to identify the APIs that are required for migration by operation group name.
		- example of operation group name: "storage account" represents PUT/GET/DELETE/PATCH/POST ... all operations related to storage account resource.
		- eventually you will have a list of operation group names: `<operation-groups> = [<operation-group-1>, <operation-group-2>, ...]`
	- No need to compile at this stage

7. **Learn extension basic**
	- Learn extension development instruction: `<project-root>\eng-docs\howto\Extension-Development-For-Private-Preview.md`
	- reference `<project-root>/src/Extensions/`, everything under except `Customization` should be considered as generated content
	
8. **Analyze API interfaces**
	- Learn Azure-PowerShell:
		- Infer path of related representation for `<operation-groups>` from `<project-root>/../azure-powershell` as `<ps-path>`
		- learn the interfaces: parameters, return types
	- Learn Azure-CLI:
		- Infer path of related representation for `<operation-groups>` from `<project-root>/../azure-cli` as `<cli-path>`
		- learn the interfaces: parameters, return types
	- Learn REST API:
		- learn API shapes of `<operation-groups>` from `<spec-path>`
	- Write summary in table `<interface-table>` thoroughly specifying comparison between API shapes, Azure PowerShell shapes and Azure CLI shapes for user to review
		- table should be able to indicate gaps, intersections, overlaps between API shapes, Azure PowerShell shapes and Azure CLI shapes
	- export `<interface-table>` to `<output-path>/interface-table.md`

9. **Analyze API implementation**
	- Learn Azure-PowerShell:
		- learn implementation of `<operation-groups>` from `<ps-path>`
	- Learn Azure-CLI:
		- learn implementation of `<operation-groups>` from `<cli-path>`
	- Learn REST API:
		- learn API usage of `<operation-groups>` from `<spec-path>`
	- Write summary in table `<implementation-table>` thoroughly specifying comparison between API usages, Azure PowerShell implementation and Azure CLI implementation for user to review
		- table should describe each usage as a workflow, for example, in PS implementation, each parameter set is a workflow, equivalent in CLI implementation as well. And Table should be able to indicate gaps, intersections, overlaps between API usages, Azure PowerShell workflows and Azure CLI workflows
	- export `<implementation-table>` to `<output-path>/implementation-table.md`
	
10. **Finalize Shapes for processors**
	- Give your recommendation from `<implementation-table>` and `<interface-table>` for minimum viable shapes+workflows for each processor and justify your choices.
	- Ask user to review your recommendation and accept their modifications as `<finalized-shapes>` and `<finalized-workflows>`
	- From the accepted workflows, identify every processor required directly or transitively by customization and orchestration.
	- Ask the user to confirm the resulting processor set, then remove generated processors outside that set. Keep unrelated generated models because they are not customer-facing and may be shared dependencies.
	- export the `<finalized-shapes>` and `<finalized-workflows>` to `<output-path>/finalized-shapes.md` and `<output-path>/finalized-workflows.md`

11. **Polish boilerplate code**
	- Delete the processor files that are not part of the selected `<operation-groups>` from `<output-path>`, confirm with user before deleting, but strongly encourage the deletion
	- Defer models that are not part of the selected `<operation-groups>` for later stages. Keep them for now but track in a list `<deferred-models>`
	- Read following links and learn the convention. Include file names, class names, namespace, assembly names and so on. You name it:
		- `<project-root>/src/Extensions/Compute/Models`
		- `<project-root>/src/Extensions/Compute/Processors`
		- `<project-root>/src/Extensions/Compute/Extensions.Compute.csproj`
	- Apply the learned conventions to the new extension code. Before starting, show your plan to user and ask for confirmation.
	- Include the missing `<project-root>/src/Extensions/Compute/Compatibility.cs` in the new extension code.
	- The generated `.csproj` has no `<Version>`: add `<Version>0.1.0</Version>` as the new extension's initial version. The release pipeline requires one and bumps it from there.
	- For data-plane APIs, locate the applicable TypeSpec `@server` declaration under `<spec-path>`.
	- Read the complete server URL or endpoint template, including template parameters, defaults, and alternative server declarations.
	- If the server is a fixed URL, expose an overridable `Endpoint` or `Url`  property, the property name should be inferred from spec. On the non-generic processor base with that URL as its default.
	- If the URL contains parameters, expose each required server parameter as a typed property and construct the endpoint from the template. Do not collapse template parameters into one opaque endpoint.
	- If multiple `@server` declarations apply, ask user to choose, fallback to the first one if empty input provided.
	- Ensure each processor combines the resolved endpoint with its operation path and that every server-template placeholder is bound.
	- No need to compile at this stage.

12. **Flatten request body in parameters**
	- Identify processors with request body as `<bodied-processors>`, each procssor should have public property (parameter) called "body" with the request type.
	- Identify all related models for `<bodied-processors>` from `<output-path>/Models` as `<required-models>`
	- Decide whether there are gaps or missing properties in `<required-models>` compared to `<finalized-shapes>`, address those properties/models accordingly by deriving from API specification strictly.
	- Flatten the properties follow `<finalized-shapes>`, nested properties all promoted to top-level parameters. Structure should respect their types found from `<output-path>/Models`
	- Remove the nested "body" property after flattening its contents into top-level parameters.
	- Implement `CreateRequestBody()` in `<bodied-processors>` to construct the request body from the flattened top-level parameters.
	- Strictly refer to examples from `<project-root>/src/Extensions/Compute/Processors` for guidance on implementing `CreateRequestBody()` and handling flattened parameters.
	- No need to compile at this stage.

13. **Write customization**
	- User can jump straight into this step if they have uncustomized extension ready. When user ask to go with step 13 directly, ask for working directory of their extension as `<output-path>`, and `<project-root>`.
	- Once get working directory, ask for what specific customization the user wants to perform. Otherwise fallback to `<output-path>/finalized-workflows.md`
	- Customization should strickly follow `<project-root>/eng-docs/howto/Extension-Development-For-Private-Preview.md` and `<project-root>/src/Extensions/Compute/Customization`
	- Customization should be placed under `<output-path>/Customization`
	- Customization folder can contain any kinds of C# code. But those classes implement or inherited processors are the customer facing "commands". That is to say, if there is a finalized workflow requires customization, there has to be a customized class implementing or inheriting the relevant processor.
	- Inherit processor only when the interface covers all parameters from the parent processor. It can have more but no less.
	- Customization should only write those entries provided by processors to wire with runtime. Otherwise should be private helper functions only.
	- When customization needs multiple processors engaged, strickly follow example how `<project-root>/src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator` use `CreateExtendedProcessor`
	- If external API needed for customization, see step 14.
	- do not compile yet

14. **Customize with external APIs engaged**
	- See `<project-root>/src/Extensions/Compute`. Creating VM requires APIs from network RP.
	- Repeat step 5, 6, 11 to create "extension" for external API. Keep variables in this session referring to the original API not external. At step 11, polish external API extensions with the original name not external name.
	- External processors don't need to be compared with PS/CLI, only keep the minimum shapes required by `<extension-name>` and perform flatten accordingly
	- Copy external prosessors and models into `<output-path>/Processors` with base processor excluded and `<output-path>/Models` respectively.
	- Make sure external processors and models behave exactly as network withs compute
	- do not compile yet

15. **Move to source**
	- Copy contents under `<output-path>` into `<project-root>/src/Extensions/<extension-name>`, create `<project-root>/src/Extensions/<extension-name>` if it does not exist, make sure `<extension-Name>` CamelCase. Exclude `<output-path>/finalized-shapes.md` and `<output-path>/finalized-workflows.md`
	- Make sure `aceconfig.yaml` is also copied into `<project-root>/src/Extensions/<extension-name>`
	- Ask user to manually merge if `<project-root>/src/Extensions/<extension-name>` exists and not empty. Hanging in prompt waiting for user provide merged path as confirmation, fallback to `<project-root>/src/Extensions/<extension-name>` if empty input provided. Create if not exists.
	- Revisit `<deferred-models>` from step 11, verify are they irrelevant indeed. Delete those that are not needed from `<project-root>/src/Extensions/<extension-name>/Models` not `<output-path>/Models`

16. **Compile**
	- Compile the merged extension to ensure there are no syntax or reference errors.
	- Try resolve any compilation or reference errors that arise.
	- If not able to perform successful compilation, ask user for manual fix and wait for confirmation before proceeding.

17. **Create PS and CLI**
	- Compose `<project-root>/src/Extensions/<extension-name>/ExtensionIndex.cs` to expose CLI/PS syntax that map to processors/orchestrators.
	- Generally speaking if one processor has a corresponding customization, that processor should be hidden, expose customization instead.
	- External API produced processors should be hidden. External API only customizations should be hidden as well if they have been referrenced in other customizations.
	- Produce corresponding PS module under `<project-root>/src/PS`, see `<project-root>/src/PS/Compute` as reference.
	- Ask user to review each sub-step and accept user modification on the fly.
	- Rebuild solution at `<project-root>` with PS project added

18. **Write test scripts**
	- Write workable tests scripts for PS under `<project-root>/tests/Scripts/PS`. See `<project-root>/tests/Scripts/PS/VMCreate.ps1` as example.
	- Write workable tests scripts for CLI under `<project-root>/tests/Scripts/CLI`. See `<project-root>/tests/Scripts/CLI/VMCreate.sh` as example.
	- Make sure any resources created in test scripts are properly cleaned up.
	- Encourage user to try test scripts

19. **Final cleanup**
	- Delete everything under `<output-path>` except `<output-path>/finalized-shapes.md` and `<output-path>/finalized-workflows.md`
	- Delete the output path for external API if any

## Output

Summarize:
- Folder structure of `<project-root>/src/Extensions/<extension-name>`
- Exposed CLI/PS commands and their mappings to processors/orchestrators, and their mapping to APIs.
- Tests and builds run, including any validation that could not be completed

## Guardrails

- Treat the current implementation as authoritative when the private-preview guide is stale.
- Keep generated-style atomic processors separate from handwritten customization code.
- Do not hand-roll authentication, retry, polling, serialization, or event publication already owned by the runtime or processor base.
- Do not expose commands or parameters that were not requested.
- Keep API versions explicit and grounded in the selected specification.
- Follow nullable C#, namespace, file layout, and naming conventions from the chosen reference extension.
- Avoid unrelated refactors and preserve public behavior outside the requested extension surface.