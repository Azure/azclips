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
    * Will be generated in the future, handwritten for now
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
* This article aims for Azclips private preview only
* Most of manual works will be replaced by CodeGen in the future
* Reference [Compute Extension](https://github.com/Azure/azclips/tree/main/src/Extensions/Compute) 
* TypeSpec is the only source format accepted by CodeGen
* Provide CodeGen either a TypeSpec project directory or an entry `.tsp` file

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
* Locate the TypeSpec project or entry `.tsp` file for the service you are trying to support
* Locate the TypeSpec operation and its request and response models. CodeGen resolves referenced model dependencies from the TypeSpec program
* Review the generated model classes under `Azclips/src/Extensions/{ExtensionName}/Models`, using `src/Extensions/Compute/Models` as the canonical example

# Develop [Processors](#processors)
* Create directory `Azclips/src/Extensions/{ExtensionName}/Processors`
* Use `src/Extensions/Compute` as the canonical example for this guide. In particular, review `src/Extensions/Compute/Processors/ComputeProcessorBase.cs`; ignore processor bases in other extensions
* Ask LLM to create `Azclips/src/Extensions/{ExtensionName}/Processors/{ExtensionName}ProcessorBase.cs`, following `ComputeProcessorBase`
* Define the generic base as `ProcessorBase<TRequest, TResponse>` because request and response schemas are independent
    * Use the request and response model types when schemas exist: `ProcessorBase<RequestModel, ResponseModel>`
    * Use `object` as the no-schema marker on either side
    * A read is `ProcessorBase<object, ResponseModel>`
    * A delete with no response body is `ProcessorBase<object, object>`
    * Do not add a one-arity convenience base
    * Give anonymous request bodies a named request model. Until such a model exists, use `object` and the full-request override described in [Miscellaneous](#miscellaneous)
* Ask LLM to create processors for the APIs you are trying to support, following the processors under `src/Extensions/Compute/Processors`; ensure public properties match the request model
* Build requests through these four protected override entries, which keep request construction and event publishing in the base:
    * `HttpMethod Method` defines the HTTP verb. It has no default; every processor must override it
    * `string CreateRequestUri()` builds the request URI. It has no default; every processor must override it
    * `TRequest? CreateRequestBody()` builds the typed body. It defaults to `null`; override it only when the API has a request body
    * `void CreateRequestHeaders(HttpRequestMessage request)` adds operation-specific headers by mutating `request`. It is a no-op by default; authentication remains the HTTP pipeline's responsibility
    * When a validated request needs a different audience or source profile, set `Core.Abstractions.Runtime.Handler.RequestAuthentication.OptionsKey` on its `HttpRequestMessage.Options` with the resolved profile and scopes. The runtime auth handler acquires the token with the publisher's cancellation token; processors must not inject bearer tokens themselves.
    * This request-scoped option leaves the shared auth handler's profile and override unchanged, so concurrent source/destination requests cannot overwrite one another's authentication state. Requests without the option retain existing profile/override behavior. Validate the endpoint and resource before setting the option, and mark secret-bearing or content-bearing requests sensitive before publication.
    * Sensitive HTTP failures keep sanitized messages and do not read response bodies. A single validated `x-ms-error-code` is available in `HttpRequestException.Data["ServiceErrorCode"]`; a single GUID `x-ms-request-id` is normalized into `Data["ServiceRequestId"]`. Missing, malformed or multiple header values are not retained. Use the status plus the service-specific code to distinguish a missing selected resource from a missing parent; do not treat every 404 as `exists=false`.
    * For guarded non-idempotent operations, optionally attach a fresh `RequestAttemptState` using its `OptionsKey`. The standard runtime retry path records transport attempts and whether a 408 or retryable 5xx response was observed; request clones retain that same state. It does not change retry policy, authorize replay or prove that a write was applied. Reconcile only against operation-specific guards and verified remote state; a plain 412 or a 429 followed by 412 is not evidence of a previously applied write.
    * Workflows that handle an expected HTTP failure may use the explicit `reportFailure: false` overload of `HttpRuntime.SendAsync` or `EnsureSuccessAsync`. Failure still throws normally and is marked with `Data["FailureDiagnosticsDeferred"]`; only the immediate ResourceOperationFailed event is deferred. The workflow must report an unhandled failure at its boundary, or report a failed reconciliation explicitly. Existing overloads retain immediate reporting. Do not use this to turn denied/network failures into success.
* Follow `GetVMProcessor` for a GET:
```csharp
public class GetVMProcessor : ComputeProcessorBase<object, VirtualMachine>
{
    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/.../{Name}?api-version=2025-11-01";
}
```
* Follow `NewVMProcessor` for a PUT with a body:
```csharp
public class NewVMProcessor : ComputeProcessorBase<VirtualMachine, VirtualMachine>
{
    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/.../{Name}?api-version=2025-11-01";

    protected override VirtualMachine? CreateRequestBody() => new VirtualMachine
    {
        Location = Location,
        Properties = new VirtualMachineProperties { /* ... */ }
    };
}
```
    * Return the request model instance directly from `CreateRequestBody()`; do not construct `HttpRequestMessage` or set `HttpContent` there
    * For long running operations (PUT/PATCH/POST/DELETE), ensure LRO properties are correctly set, see https://github.com/Azure/azclips/blob/c606d4ea85b60eb0499811faa5b1310422fd9c5d/src/Extensions/Compute/Processors/ComputeProcessorBase.cs#L77-L85
* Understand the base-owned processing lifecycle:
    * <a id="sendasync"></a>`SendAsync()` creates and sends the request and returns the response. Override it when a customization composes multiple processors or needs different send behavior
    * <a id="processasync"></a>`ProcessAsync()` calls `BeforeProcessAsync()`, publishes the start event, calls `SendAsync()`, deserializes the response, handles output, and publishes the end event exactly once from `finally`
    * Empty or whitespace content publishes `ResponseEmptyBody` and skips deserialization
    * Invalid JSON publishes `ResponseInvalidJson` and throws `InvalidOperationException`
* Reuse these processing override entries instead of replacing the whole lifecycle:
    * <a id="overridebeforeprocessbase"></a>`partial void OverrideBeforeProcessBase()` is the synchronous, base-wide injection point. Implement it in a companion partial file to affect every processor
    * `Task BeforeProcessAsync()` is the per-processor injection point. An override replaces the default; call `base.BeforeProcessAsync()` when the base-wide injection should also run
    * `Task<bool> BeforeWriteOutputAsyncAndReturn(TResponse? body)` runs before the default output write. Return `false` to let the base write a non-null body. To customize or omit output, perform any required output work in the override and return `true`

# Develop Customization
* Create directory `Azclips/src/Extensions/{ExtensionName}/Customization`
* Customization is intentionally flexible. There is no required class name, filename suffix, or naming convention
    * Compute uses names such as `NewVMProcessor_Orchestrator`; `_Orchestrator` is only the name chosen by that implementation, not a convention for new customization
    * New customization may use any clear, arbitrary name
    * Follow the patterns under `src/Extensions/Compute/Customization` as examples, but reuse the processor override entries described in [Develop Processors](#develop-processors) instead of creating a separate framework
* Identify APIs to customize, for example minimal creation for `vm` requires `vnet`, `nsg`, `nic` from Network RP
* Create processors for APIs, see [develop processors](#develop-processors)

## Multi-API Customization
* Create a class with an appropriate arbitrary name under `Azclips/src/Extensions/{ExtensionName}/Customization`
* Specify both request and response generic types. Use the request and response models for the final API, or `object` where no schema exists
* Recurse [Multi-API Customization](#multi-api-customization) for required customization if needed. For example, Compute's NIC creation is itself a multi-API customization
* Ask LLM to compose public properties, which should be the union of all APIs (processors) in this customization
* Override [SendAsync()](#sendasync)
    * Use `CreateExtendedProcessor<T>` to create the processors used by the customization
        * `T` is the processor type
        * Map public properties from the customization to processors
    * Execute [`SendAsync()`](#sendasync) for each processor, sequentially or in parallel, except the final processor
    * `return await {FinalProcessor}.SendAsync();`
    * You can ask LLM to do it but I suggest try with bare hands.

## Customization with no API calls
* Create an arbitrarily named class under `Azclips/src/Extensions/{ExtensionName}/Customization`
* Use `object` for request or response types that have no schema
* Arbitrarily compose public properties
* Override [ProcessAsync()](#processasync) to implement the custom operation
* For example, a customization to connect ACR
    * public properties: ACREndpoint + UserName + Password
    * `ProcessAsync`: `docker login {ACREndpoint} -u {UserName} -p {Password}`

## Customization with computation
* Imagine API `A` requires property `P`, value of `P` can only be retrieved at runtime from DB by string `k`
* Let's define a computation `P = Func(K)`
* Create an arbitrarily named class under `Azclips/src/Extensions/{ExtensionName}/Customization`
* Specify both the request and response model types of `A`
* Copy the entire content from processor A to customization A
* Replace public property `P` with `K`
* Assign `P = Func(K)` in `CreateRequestUri()` or `CreateRequestBody()`, whichever consumes `P`

## Miscellaneous
Customization has no restrictions beyond deriving from the processor base and reusing its override entries where they fit. The full-control escape hatches are:
* <a id="createrequestmessage"></a>Override `CreateRequestMessage()` for request construction that cannot use `Method`, `CreateRequestUri()`, `CreateRequestBody()`, and `CreateRequestHeaders()`. This bypasses those entries and the base's request-creation event wiring, so publish any required events in the override
* Override [SendAsync()](#sendasync) to compose processors or replace send behavior
* Implement [`OverrideBeforeProcessBase()`](#overridebeforeprocessbase) in a companion partial file when synchronous setup must run before `ProcessAsync()` for every processor. Use `BeforeProcessAsync()` instead when the behavior applies to only one processor
* Override [ProcessAsync()](#processasync) only for a completely custom operation. This bypasses `OverrideBeforeProcessBase()`, `BeforeProcessAsync()`, `BeforeWriteOutputAsyncAndReturn()`, and the base-owned processing lifecycle, so preserve required events and output behavior in the override

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
    * `Import-module Azclips\src\PS\Accounts\Accounts.psd1`
    * `Import-module Azclips\src\PS\{ModuleName}\{ModuleName}.psd1`
    * Try run PS cmdlets

# Add Test Scripts
Persist manual verification scripts in
* `Azclips/tests/Scripts/CLI`
* `Azclips/tests/Scripts/PS`
