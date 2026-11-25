# Terminology
* <a id="extension"></a>Extension: Microsoft.Azure.Azclips.Extensions.{ExtensionName}
    * .Net project
    * [Models](#models)
    * [Processors](#processors)
    * [Customization](#customization)
    * [ExtensionIndex](#extensionindex)
* <a id="models"></a>Models: 
    * C# class representation of API schema
    * Self-contained: Serialization/Deserialization
    * Auto-generated
* <a id="processors"></a>Processors: 
    * Programmable representation of Azure operations
    * Implemented `IProcessor` interface, provide method `ProcessAsync()`
    * Classes under directory `Processors` indicate atomic(generated) operations
* <a id="customization"></a>Customization:
    * Handwritten representation of customized Azure or other operations
    * Classes under directory `Customization` indicate customized (handwritten) operations
* <a id="extensionindex"></a>ExtensionIndex:
    * Dictionary representation of complete syntax extracted from extension.
    * Will be generated at build-time in the future, manually maintained for now
    * Will be conditionally compiled for different runtime in the future, mixed with all flavors for now
    * Source of syntax will be flavored attributes (produced by CodeGen) in the future, manually composed for now

# Background
* This article instructs how to develop extensions manually or agentically
* Most of manual works will be replaced by CodeGen in the future
* Control plane reference [Compute](https://github.com/Azure/azclips/tree/main/src/Extensions/Compute) 
* Data plane reference [Storage Blob](https://github.com/Azure/azclips/tree/main/src/Extensions/Storage/Blob) 
* TypeSpec is the only source format accepted by CodeGen

# Create a new Extension Project
* Build local codegen tool
```
cd Azclips/toolkit
./build.ps1
```
* Create a new directory `Azclips/src/Extensions/{ExtensionName}`
* Create a new file `Azclips/src/Extensions/{ExtensionsName}/aceconfig.yaml`
* Sample content 
```
schema-version: 1

# Run inputs: the CLI copies the spec; tsp-emit compiles each entrypoint.
output-dir: .                            # default: this file's folder
tsp-location: https://github.com/Azure/azure-rest-api-specs/tree/main/specification/containerservice/resource-manager/Microsoft.ContainerService
# additional-directories:                # folders the compile needs, not under tsp-location
entrypoints:                             # relative to tsp-location
  - path: ./aks
    api-version: 2026-09-01

# Document facts: what the spec cannot provide; seed-ir writes them into the IR's target.
service-name: ContainerService
root-namespace: Microsoft.Azure.AzClips.Extensions.ContainerService
project-name: Microsoft.Azure.AzClips.Extensions.ContainerService
description: Container service extension for AzClips
```
* Generate extension
```
cd Azclips/src/Extensions/{ExtensionsName}
node Azclips/toolkit\packages\cli\dist\src\cli.js compile
```
* For extensions that require multiple API spec sources, follow the [Storage Blob](https://github.com/Azure/azclips/tree/main/src/Extensions/Storage/Blob) example. We will support emit multiple source from the aceconfig.yaml soon and update this document accordingly.
* Add `Azclips/src/Extensions/{ExtensionName}/{project-name}.csproj` to `Azclips/azclips.slnx`, replacing `{project-name}` with the value from `aceconfig.yaml` (for this sample, `Microsoft.Azure.AzClips.Extensions.ContainerService`).
* Try compiling `dotnet build Azclips/src/Extensions/{ExtensionName}/{project-name}.csproj`, using that same project name; contact the Azclips team if any blockers are encountered.

# Develop [Models](#models)
* Review the generated model classes under `Azclips/src/Extensions/{ExtensionName}/Models`
* Don't edit anything under this directory, write customized models under `Azclips/src/Extensions/{ExtensionName}/Customization` if needed
* All properties under any models should have flattened public members, contact Azclips team to report bugs otherwise

# Develop [Processors](#processors)
* Review the generated model classes under `Azclips/src/Extensions/{ExtensionName}/Processors`
* Don't edit anything under this directory, write customized models under `Azclips/src/Extensions/{ExtensionName}/Customization` if needed
* All public properties should only be primitive types or collections (lists, dictionaries), contact Azclips team to report bugs otherwise
* Understand `Processorbase` in detail, carefully review each protected virtual, partial members

# Develop Customization
* Create directory `Azclips/src/Extensions/{ExtensionName}/Customization`
* Customization is intentionally flexible. There is no required class name, filename suffix, or naming convention
    * Compute uses names such as `NewVMProcessor_Orchestrator`; `_Orchestrator` is only the name chosen by that implementation, not a convention for new customization
    * New customization may use any clear, arbitrary name
* Identify APIs to customize, for example minimal creation for `vm` requires `vnet`, `nsg`, `nic` from Network RP

## Hooks to Customize in Processor Base
Hooks come in two kinds:
* **`protected virtual` members**: override them in one processor, either through a partial class or a child class. Call `base` when you only want to add to the default.
* **`partial void Override*Base` methods**: implement them once in a companion partial `ProcessorBase` file under `Customization`. They apply to every processor in the extension. Each can have only one implementation, so branch on type or state inside it (see `Storage/Blob/Customization/ProcessorBase.Content.cs`).

### Lifecycle
`Init()` → `ProcessAsync()` → `BeforeProcessAsync()` → `SendAsync()` → `CreateRequestMessage()` → `DeserializeResponseAsync()` → `BeforeWriteOutputAsyncAndReturn()` → `WriteOutput`

### Hook reference
| Stage | Hook | Kind | Use it to |
|---|---|---|---|
| Before process | `OverrideBeforeProcessBase()` | partial, on `ProcessorBase<TRequest, TResponse>` | Run setup for every processor, such as auth (see [Customize Auth Flow](#customize-auth-flow)) |
| Before process | `BeforeProcessAsync()` | virtual | Override before process on single class, usually when `OverrideBeforeProcessBase()` need to be override for some child classes |
| Request | `Endpoint` | virtual | Redirect the base URL for one processor or the whole extension (Storage's `ResolveUrl()`) |
| Request | `Method`, `CreateRequestUri()` | virtual | Set the HTTP method and path. Generated processors already override these |
| Request | `CreateRequestBody()` | virtual | Return the request body. Return `null` for no body |
| Request | `CreateRequestContent(body)` / `OverrideCreateRequestContentBase()` | virtual / partial | Send a non-JSON body, such as raw bytes or XML |
| Request | `CreateRequestHeaders(request)` | virtual | Add headers. Call `base` to keep the generated headers |
| Request | `CreateRequestMessage()` | virtual | Build the whole request yourself. This bypasses the hooks above and the before/after request-create events, so publish those events yourself |
| Send | `SendAsync()` | internal virtual | Compose child processors or replace how the request is sent |
| Send | `SendAsyncLRO()` + `GetFinalUri`, `GetOperationLocationUri`, `GetAsyncOperationUri`, `GetLocationUri` | internal virtual / virtual | Poll a long-running operation: override `SendAsync() => SendAsyncLRO()` and the URI getters |
| Response | `DeserializeResponseAsync(rawBody)` / `OverrideDeserializeResponseBase()` | virtual / partial | Parse a non-JSON response, such as Storage's XML. The partial wins when it sets `returnNow` |
| Output | `BeforeWriteOutputAsyncAndReturn(body)` | virtual | Shape or page the output. Return `true` to suppress the default `WriteOutput` (see `WriteOutputWithPaginationAsync`) |
| Process | `ProcessAsync()` | virtual | Run a fully custom operation (see below) |

### Escape hatches
* **Overriding `ProcessAsync()`** replaces the whole base lifecycle. Keep the base behaviour you need:
  * Call `await BeforeProcessAsync()` first, or auth set up in `OverrideBeforeProcessBase()` is skipped.
  * Publish `Events.ProcessorProcessAsyncStart`, and publish `Events.ProcessorProcessAsyncEnd` in a `finally` block.
  * Write the result yourself with `WriteOutput`.
  * See `Storage/Blob/Customization/UploadBlockBlobProcessor_Orchestrator.cs`.
* **Child processors:** a processor created with `CreateExtendedProcessor<T>()` and called with `SendAsync()` never runs its own `BeforeProcessAsync()`. It shares the parent's `HttpPipeline`, `AuthHandler`, `Publisher` and `WriteOutput`, so pipeline steps the parent added still apply. To customize only the child's request, use a private nested subclass that overrides `CreateRequestHeaders()` and calls `base`.
* **Older scaffolds:** an extension's base may not have the content or deserialize hooks yet (Network doesn't). Regenerate the extension rather than editing `Processors/ProcessorBase.cs`.

## Derive customized processors
* In most cases, a customized processor need to inherit the `ProcessorBase`, or any child classes of `ProcessorBase`
    * Partial class of processors: customize partial when you only need **additional** public properties, helper functions
    * Child class of processors: inherit processors when you need to override **some** protected members, a child class can also have **additional** public properties, helper functions 
    * Partial class of processor base: when you need to implement customized behavior for all processors, see [Customize Auth Flow](#customize-auth-flow) or additional shared public properties, helper functions
    * Child class of processor base: inherit processor base when you need to
        * Extract public members of processors, redefine public members, and use `CreateExtendedProcessor<T>` to reuse processors, see [Customization with Computation](#customization-with-computation)
        * Orchestrate multiple API calls, similar as extracting public members, see [Multi-API Orchestration](#multi-api-orchestration)
        * Override entire process, see [Customization With No API Calls](#customization-with-no-api-calls)

## Multi-API Orchestration
* Create a class with an appropriate arbitrary name under `Azclips/src/Extensions/{ExtensionName}/Customization` and inherit `processorbase`
* Specify both request and response generic types. Use the request and response models for the final API, or `object` where no schema exists
* Recurse [Multi-API Customization](#multi-api-customization) for required customization if needed. For example, Compute's NIC creation is itself a multi-API customization
* Combine public properties from APIs, expose what are needed and drop what are unnecessary
* Override [SendAsync()](#sendasync)
    * Use `CreateExtendedProcessor<T>` to create the processors used by the customization
        * `T` is the processor type
        * Map public properties from the customization to processors
    * Execute [`SendAsync()`](#sendasync) for each processor, sequentially or in parallel, except the final processor
    * `return await {FinalProcessor}.SendAsync();`
    * See [VM Create](https://github.com/Azure/azclips/blob/main/src/Extensions/Compute/Customization/NewVMProcessor_Orchestrator.cs)

## Customization with no API Calls
* Create an arbitrarily named class under `Azclips/src/Extensions/{ExtensionName}/Customization` and inherit `processorbase` 
* Use `object` for request or response types that have no schema
* Arbitrarily compose public properties
* Override [ProcessAsync()](#processasync) to implement the custom operation
* For example, a customization to connect ACR
    * public properties: ACREndpoint + UserName + Password
    * `ProcessAsync`: `docker login {ACREndpoint} -u {UserName} -p {Password}`

## Customization with Computation
* Imagine API `A` requires property `P`, value of `P` can only be retrieved at runtime from DB by string `k`
* Let's define a computation `P = Func(K)`
* Create an arbitrarily named class under `Azclips/src/Extensions/{ExtensionName}/Customization` and inherit `processorbase`
* Specify both the request and response model types of `A`
* Copy the entire content from processor A to customization A
* Replace public property `P` with `K`
* Assign `P = Func(K)` in `CreateRequestUri()` or `CreateRequestBody()`, whichever consumes `P`

## Customize Auth Flow
* For data plane APIs do not follow standard ARM auth flow, we provided dedicated hook to implement the customized auth handling
* There is a standard pattern data plane APIs used to exchange token by ARM access token, if the API had followed the ARM standard:
    * Implement the partial method `OverrideBeforeProcessBase` from processor base
    * From `ProcessorBase.AuthHandler`, provide the data plane audience, to impelement a token acquirer delegate
    * Assign the delegate to `ProcessorBase.AuthHandler.AcquireTokenAsyncOverride`
    * Assign `true` to `ProcessorBase.AuthHandler.ApplyOverride`
    * See [MSGraph Auth](https://github.com/Azure/azclips/blob/main/src/Extensions/MsGraph/Customization/GraphProcessorBase.cs)
    * This entire flow will be configurable rather than customization soon
* For unconventional data plane auth flow, apply customized auth header arbitrarily by implementing `OverrideBeforeProcessBase` from processor base. See [Storage blob auth](https://github.com/Azure/azclips/blob/main/src/Extensions/Storage/Blob/Customization/ProcessorBase.cs)

# Develop ExtensionIndex
* Will introduce configuration and attribute based syntax soon to really derive the index from extension code. Use LLM or manually update now to maintain the index.
* Ask LLM to create `Azclips/src/Extensions/{ExtensionName}/ExtensionIndex.cs`, by reviewing what you already have in `Azclips/src/Extensions/{ExtensionName}` and `Azclips/src/Extensions/Compute` as reference
* For now, feel free to hide/expose any interfaces (command, parameters) however you want
* Review `s_psTable`, `s_cliTable` 

# Develop PowerShell Module
Ask LLM to review `Azclips/src/Extensions/{ExtensionName}/ExtensionIndex.cs` and create a PS module under `Azclips/src/PS`, take `Azclips/src/PS/Compute` as reference

# Manual Verification
* Go to root of `Azclips`
* `dotnet build azclips.slnx`
* For CLI, `Azclips\artifacts\Debug\Core.Cli\az.exe {CommandGroupName} {Command} ...`
* For PowerShell
    * `Import-module Azclips\src\PS\Accounts\Accounts.psd1`
    * `Import-module Azclips\src\PS\{ModuleName}\{ModuleName}.psd1`
    * Try run PS cmdlets

# Add Test Scripts
* Implement and record scenario tests
* See [Scenario Test](https://github.com/Azure/azclips/blob/main/tests/Scenario.Tests/README.md)
* See [Storage Scenario Test](https://github.com/Azure/azclips/tree/main/tests/Scenario.Tests/Storage)