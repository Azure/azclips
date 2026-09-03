# Terminology
* <a id="extension"></a>Extension: Microsoft.Azure.Azclips.Extensions.{ExtensionName}
    * .Net project
    * [Models](#models)
    * [Processors](#processors)
    * [Orchestrators](#orchestrators)
    * [ExtensionIndex](#extensionindex)
* <a id="models"></a>Models: 
    * C# class representation of API schema
    * Self-contained: Serialization/Deserialization
    * Will be generated in the future, handwritten for now
* <a id="processors"></a>Processors: 
    * Programmable representation of Azure operations
    * Implemented `IProcessor` interface, provide method `ProcessAsync()`
    * Classes under directory `Processors` indicate atomic(generated) operations
* <a id="Orchestrators"></a>Orchestrators: 
    * Conceptual representation of Azure or any operations
    * Classes under directory `Orchestrators` indicate customized(handwritten) operations
* <a id="extensionindex"></a>ExtensionIndex:
    * Dictionary representation of complete syntax extracted from extension.
    * Will be generated at build-time in the future, manually maintained for now
    * Will be conditionally compiled for different runtime in the future, mixed with all flavors for now
    * Source of syntax will be flavored attributes (produced by CodeGen) in the future, manually composed for now

# Background
* This article instructs how to develop extensions manually or agentically
* This article aims for Azclips private preview only
* Most of manual works will be replaced by CodeGen in the future
* Reference [Compute Extension](https://github.com/Azure/azclips/tree/main/src/Extensions/Compute) 
* Azure APIs have swagger and TypeSpec as sources, to make it easier for LLM, use swagger as spec

# Create a new Extension Project
* Create a new directory `Azclips/src/Extensions/{ExtensionName}`
* Create a new file `Azclips/src/Extensions/{ExtensionsName}/Extensions.{ExtensionName}.csproj`
* Add `Azclips/src/Extensions/{ExtensionsName}/Extensions.{ExtensionName}.csproj` to `Azclips/azclips.slnx`
* Content 
```
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <AssemblyName>Microsoft.Azure.Azclips.Extensions.{ExtensionName}</AssemblyName>
        <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
        <RootNamespace>Microsoft.Azure.Azclips.Extensions.{ExtensionName}</RootNamespace>
        <ExtName>Microsoft.Azure.Azclips.Extensions.{ExtensionName}</ExtName>

        <Description>{ExtensionName} Extension for Azclips CLI</Description>
        <PackageLicenseExpression>MIT</PackageLicenseExpression>
        <RepositoryUrl>https://github.com/Azure/azclips</RepositoryUrl>

        <PackageId>Microsoft.Azure.Azclips.Extensions.{ExtensionName}</PackageId>
        <Version>1.0.0-alpha</Version>
        <Authors>Microsoft Corporation</Authors>
        <Company>Microsoft Corporation</Company>
        <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
        <EnableDynamicLoading>true</EnableDynamicLoading>
    </PropertyGroup>

    <Import Project="$(MSBuildThisFileDirectory)..\..\..\Shared.props" />

    <ItemGroup>
        <ProjectReference Include="..\..\Core\Core.Abstractions\Core.Abstractions.csproj" >
            <Private>false</Private>
            <ExcludeAssets>runtime</ExcludeAssets>
        </ProjectReference>
        <ProjectReference Include="..\ExtensionRuntime\ExtensionRuntime.csproj" >
            <Private>false</Private>
            <ExcludeAssets>runtime</ExcludeAssets>
        </ProjectReference>
    </ItemGroup>
</Project>
```

# Develop [Models](#models)
* Create directory `Azclips/src/Extensions/{ExtensionName}/Models`
* Locate the API you are trying to support, find the schema of request/response, for example:
    * Locate API for [`POST: "/subscriptions/{subscriptionId}/providers/Microsoft.AzureTerraform/exportTerraform"`](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/terraform/resource-manager/Microsoft.AzureTerraform/preview/2025-09-01-preview/export.json#L89)
    * Find schema names for [request schema Id](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/terraform/resource-manager/Microsoft.AzureTerraform/preview/2025-09-01-preview/export.json#L110) and [response schema Id](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/terraform/resource-manager/Microsoft.AzureTerraform/preview/2025-09-01-preview/export.json#L118)
    * Find schemas by schema Ids: [request schema](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/terraform/resource-manager/Microsoft.AzureTerraform/preview/2025-09-01-preview/export.json#L191), [response schema](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/terraform/resource-manager/Microsoft.AzureTerraform/preview/2025-09-01-preview/export.json#L410)
* Ask LLM to create model classes for request schema/response schema under `Azclips/src/Extensions/{ExtensionName}/Models`, take `Azclips/src/Extensions/Compute` as reference
* Ask LLM to resolve and create model classes for all dependency schemas under `Azclips/src/Extensions/{ExtensionName}/Models`

# Develop [Processors](#processors)
* Create directory `Azclips/src/Extensions/{ExtensionName}/Processors`
* Understand `ProcessorBase` by reviewing `src/Extensions/Compute/Processors/ComputeProcessorBase.cs`. You can ignore most properties/methods but please ensure get familiar with below 3 methods:
    * <a id="createrequestmessage"></a>`CreateRequestMessage()`
        * Access modifier: `protected` indicates only used by child classes
        * This method defines what request this processor sends
        * Empty in base class, implemented in child classes
    * <a id="sendasync"></a>`SendAsync()`
        * Access modifier: `internal` indicates only used inside this extension assembly
        * This method defines what response this processor receives
        * Implemented by base class
    * <a id="processasync"></a>`ProcessAsync()`
        * Access modifier: `public` indicates used by external callers
        * This method defines how this processor processes
        * Implemented by base class
* Ask LLM to create `Azclips/src/Extensions/{ExtensionName}/Processors/{ExtensionName}ProcessorBase.cs`, take `Azclips/src/Extensions/Compute` as reference
* Ask LLM to create processors for APIs you are trying to support, take `Azclips/src/Extensions/Compute` as reference, ensure public properties match the request model class
    * Ensure generic type is the response type of the API
    * For Delete API, the generic type should be object
    * For long running operations (PUT/PATCH/POST/DELETE), ensure LRO properties are correctly set, see https://github.com/Azure/azclips/blob/c606d4ea85b60eb0499811faa5b1310422fd9c5d/src/Extensions/Compute/Processors/ComputeProcessorBase.cs#L77-L85
    * Ensure [`CreateRequestMessage`](#createrequestmessage) matches the API
        * For APIs with request body (PUT/PATCH/POST), ensure [`CreateRequestMessage`](#createrequestmessage) wire public properties to request correctly

# Develop Orchestrators
* Create directory `Azclips/src/Extensions/{ExtensionName}/Orchestrators`
* Identify APIs to be orchestrated, for example minimal creation for `vm` requires `vnet`, `nsg`, `nic` from Network RP
* Create processors for APIs, see [develop processors](#develop-processors)

## Orchestrators with Multi-API
* Create `Azclips/src/Extensions/{ExtensionName}/Customization/(APIName)Processor_Orchestrator.cs`
* Generic type is the model class of the final API
* Recurse [Multi-API Orchestrator](#orchestrators-with-multi-api) for required orchestrators if needed `Azclips/src/Extensions/{ExtensionName}/Customization/(RequiredOrchestratorName)Processor_Orchestrator.cs`, for example nic creation is multi-API orchestrator
* Ask LLM to compose public properties, should be union of all APIs (processors) in this orchestrator
* Override [SendAsync()](#sendasync)
    * Use `CreateExtendedProcessor<T>` to create orchestrated processors
        * `T` is the type of orchestrated processor
        * Map public properties from orchestrator to processors
    * Execute [`SendAsync()`](#sendasync) of each processors, sequentially or in parallel, except the processor for the generic type of orchestrator
    * `return await {FinalProcessor}.SendAsync();`
    * You can ask LLM to do it but I suggest try with bare hands.

## Orchestrators with no API calls
* Create `Azclips/src/Extensions/{ExtensionName}/Customization/(ArbitraryName)Processor_Orchestrator.cs`
* Generic type is `object`
* Arbitrarily compose public properties
* Override [ProcessAsync()](#processasync), orchestrate arbitrarily
* For example, an orchestrator to connect ACR
    * public properties: ACREndpoint + UserName + Password
    * `ProcessAsync`: `docker login {ACREndpoint} -u {UserName} -p {Password}`

## Orchestrators with computation
* Imagine API `A` requires property `P`, value of `P` can only be retrieved at runtime from DB by string `k`
* Let's define a computation `P = Func(K)`
* Create `Azclips/src/Extensions/{ExtensionName}/Customization/(A)Processor_Orchestrator.cs`
* Generic type is the model class of `A`
* Copy entire content from processor A to orchestrator A
* replace public property `P` with `K`
* In [CreateRequestMessage()](#createrequestmessage), assign `P = Func(k);`

## Miscellaneous
Improvise overriding [CreateRequestMessage()](#createrequestmessage), [SendAsync()](#sendasync) and [ProcessAsync()](#processasync)

# Develop ExtensionIndex
* Ask LLM to create `Azclips/src/Extensions/{ExtensionName}/ExtensionIndex.cs`, by reviewing what you already have in `Azclips/src/Extensions/{ExtensionName}` and `Azclips/src/Extensions/Compute` as reference
* Review `s_psTable`, `s_cliTable`, delete entries you don't want to expose

# Register CLI Command Group
For CLI, map command group name to extension name in: `Azclips/src/CLI/Parser/CommandGroupIndex.cs`

# Develop PowerShell Module
Ask LLM to review `Azclips/src/Extensions/{ExtensionName}/ExtensionIndex.cs` and create a PS module under `Azclips/src/PS`, take `Azclips/src/PS/Compute` as reference

# Manual Verification
* Go to root of `Azclips`
* `dotnet build azclips.slnx`
* For CLI, `Azclips\artifacts\Debug\Core.Cli\az.exe {CommandGroupName} {Command} ...`
* For PowerShell
    * `Import-module Azclips\src\PS\Accounts\accounts.psd1`
    * `Import-module Azclips\src\PS\{ModuleName}\{ModuleName}.psd1`
    * Try run PS cmdlets

# Add Test Scripts
Persist manual verification scripts in
* `Azclips/tests/Scripts/CLI`
* `Azclips/tests/Scripts/PS`
* No need to guarantee the script files runnable, guarantee content of scripts are runnable

# TODO
1. **Update the [Processors](#processors) and [Orchestrators](#orchestrators) sections for the new request-construction seams.**
    * **Why:** `ComputeProcessorBase` no longer expects child processors to override `CreateRequestMessage()`. That method is now a sealed-in-practice template on the base that owns event publishing (`ProcessorBeforeHttpRequestCreate` / `ProcessorAfterHttpRequestCreate`) and JSON body serialization. It was split into three smaller override seams so developers don't re-wire the event publisher for every processor:
        * `protected virtual HttpMethod Method` — the HTTP verb. **No default** (throws `NotImplementedException`); every processor must override it (e.g. `=> HttpMethod.Get` / `HttpMethod.Put` / `HttpMethod.Delete`).
        * `protected virtual string CreateRequestUri()` — builds the request URI. **No default** (throws); override in every processor.
        * `protected virtual object? CreateRequestBody()` — builds the request body object, serialized by the base with `JsonOptions` using the object's runtime type. **Defaults to `null`** (no content); override only for PUT/PATCH/POST create/update operations.
    * **What to change in this doc:**
        * In [Develop Processors](#develop-processors), replace guidance that says "implement `CreateRequestMessage()`" with the three seams above. Show a GET example (override `Method` + `CreateRequestUri`) and a PUT example (override `Method` + `CreateRequestUri` + `CreateRequestBody`). Note that `CreateRequestBody` must return the model instance directly — do **not** build an `HttpRequestMessage` or set `HttpContent` by hand.
        * In [Orchestrators with computation](#orchestrators-with-computation), the "assign `P = Func(k)` in `CreateRequestMessage()`" step should point at `CreateRequestUri()` / `CreateRequestBody()` instead (whichever consumes `P`).
        * In [Miscellaneous](#miscellaneous), keep `CreateRequestMessage()` listed as the full-control escape hatch: overriding it directly is still supported and bypasses the three seams (and the event wiring), so existing/handwritten processors that override `CreateRequestMessage()` are unaffected.
    * **Reference implementation:** `src/Extensions/Compute/Processors` — `GetVMProcessor` (GET), `DeleteVMProcessor` (DELETE), `NewVMProcessor` (PUT with body). All Compute processors were migrated to the new seams; use them as the canonical pattern.
    * **Scope note:** only Compute's `ComputeProcessorBase` was refactored. Other extensions (MsGraph, Resources, Login) still use their own `CreateRequestMessage()`-based bases; update those bases and their docs separately if/when this pattern is adopted repo-wide.
2. **Update the [Processors](#processors) section for the new `ProcessAsync()` lifecycle seams.**
    * **Why:** `ComputeProcessorBase<TRequest, TResponse>.ProcessAsync()` is still the base-owned pipeline (publish start → `SendAsync()` → read/deserialize body → write output → publish end, all inside one `try/finally` that publishes `ProcessorProcessAsyncEnd` exactly once). It was split to add two override seams so developers can inject cross-cutting behavior and control output writing without re-implementing the whole pipeline or re-wiring events:
        * `protected virtual Task BeforeProcessAsync()` — runs first, before the `ProcessorProcessAsyncStart` event (inside the `try`, so its exceptions still publish end). Two tiers behind it:
            * `partial void OverrideBeforeProcessAsyncBase()` — **base-wide** injection point. It is a classic (erasable) partial method, so it must be `void`/synchronous and needs no body until you want one; implement it once in a companion `partial` file of `ComputeProcessorBase<TRequest, TResponse>` to apply logic to **every** processor. (Non-void `partial Task` is not allowed to be unimplemented, hence the sync signature.)
            * `BeforeProcessAsync()` itself is `virtual` — the **per-processor** override point. Overriding it replaces the default; call `base.BeforeProcessAsync()` if you want the base-wide injection to still run. This "drop base by not calling it / keep base by calling it" is by design.
        * `protected virtual Task BeforeWriteOutputAsync(TResponse? body, ref bool returnNow)` — runs after deserialization, immediately before the default write. `body` may be `null` (no content / empty body / null deserialize). Set `returnNow = true` to **omit** the default write; to **customize** output, call the inherited `WriteOutput(...)` yourself and set `returnNow = true`. Default is a no-op, so the base writes `SelectOutput(body)` when `body` is non-null.
    * **Preserved pipeline contracts (do not regress):** empty/whitespace body publishes `ResponseEmptyBody` and skips deserialization; invalid JSON publishes `ResponseInvalidJson` then throws `InvalidOperationException`; `ProcessorProcessAsyncEnd` fires once on every path (including early/no-content and error paths).
    * **What to change in this doc:** in [Develop Processors](#develop-processors) / [Miscellaneous](#miscellaneous), document these two hooks alongside `SelectOutput()` (which still answers "*what* to write"), and make clear the base-wide (`partial`) vs per-processor (`virtual`) distinction. Overriding `ProcessAsync()` wholesale remains the full-control escape hatch and bypasses both hooks.
    * **Reference implementation:** `src/Extensions/Compute/Processors/ComputeProcessorBase.cs` (`ProcessAsync`, `BeforeProcessAsync`, `OverrideBeforeProcessAsyncBase`, `BeforeWriteOutputAsync`). No Compute processor overrides these yet — they are dormant no-op defaults, so existing processors are unaffected.
    * **Scope note:** Compute-only, same as item 1.
3. **Add a `CreateRequestHeaders(HttpRequestMessage)` seam to the request-construction template.**
    * **Why:** the item-1 seams (`Method` / `CreateRequestUri` / `CreateRequestBody`) cannot express **custom request headers**, which some processors need (e.g. Microsoft Graph advanced queries set the `ConsistencyLevel` header — see `src/Extensions/MsGraph/Processors/ListUserProcessor.cs` and the test `ListUserProcessorTests.CreateRequestMessage_ConsistencyLevel_ShouldBeHeaderNotQuery`). Without a header seam, such processors are forced back onto the full `CreateRequestMessage()` escape hatch, defeating the split.
    * **Shape:**
        * `protected virtual void CreateRequestHeaders(HttpRequestMessage request) { }` — no-op default; override to mutate the request (`request.Headers.Add(...)`, `request.SetConsistencyLevel(...)`). Void + mutate (the request is mutable); synchronous (header values are bound properties/constants — auth tokens remain the pipeline's concern). Lives on the non-generic base next to `Method`/`CreateRequestUri`/`CreateRequestBody`.
        * The base `CreateRequestMessage()` template calls it after the body is attached and before the `ProcessorAfterHttpRequestCreate` event:
            ```csharp
            var request = new HttpRequestMessage(Method, CreateRequestUri());
            if (CreateRequestBody() is { } body) request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
            CreateRequestHeaders(request);
            ```
    * **What to change in this doc:** in [Develop Processors](#develop-processors), list `CreateRequestHeaders` as the fourth request seam (override only when the operation needs headers beyond what the pipeline adds).
    * **Scope note:** add wherever the request-construction template is adopted (Compute today; MsGraph/Resources as they migrate).
4. **Split the processor base generic into `Base<TRequest, TResponse>` (request and response are independent schemas).**
    * **Why:** the current single-generic base (`Base<TResponse>`) with `CreateRequestBody()` returning `object?` bakes in the assumption that the request body shares the response schema. That is only coincidentally true (e.g. VM PUT) and wrong in general (POST actions, LRO). The request body should be a first-class typed schema, symmetric to the response.
    * **Shape:**
        * The base is two-arity: `Base<TRequest, TResponse>`, where the two-arity generic covariantly overrides the non-generic template's `object? CreateRequestBody()` with a typed `protected override TRequest? CreateRequestBody() => default;` (no body → `null`). Write processors override it to return their `TRequest` model.
        * `object` is the "no schema" marker on **both** sides, exactly as `object` already means "no response schema" for deletes. So a read is `Base<object, TResponse>` and a delete is `Base<object, object>`.
        * **No 1-arity shim.** Every processor and orchestrator spells out both type arguments explicitly. Carrying the request/response types is a codegen concern — the generated schema names both naturally — so the extension layer must not paper over it with a convenience base.
    * **Note on anonymous bodies:** processors that currently post anonymous objects (e.g. `new { groupIds = … }`) get real, named request schemas from codegen; naming them is codegen's concern, not the base's. Until then they can stay on `object`/the escape hatch.
    * **Contract impact:** the two-arity base changes the class-declaration line of every subclass — reads become `<object, TResponse>`, deletes `<object, object>`, create processors/orchestrators `<Model, TResponse>`. Update every subclass (including orchestrators and tests) and verify they compile and behave.
    * **Status:** implemented in Compute (base, all processors, orchestrators, and the subscription-resolution test). Apply the same to MsGraph/Resources during their migration.