# Terminology
* <a id="extension"></a>Extension: Microsoft.Azure.Cirrus.Extensions.{ExtensionName}
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
* This article aims for Cirrus private preview only
* Most of manual works will be replaced by CodeGen in the future
* Reference [Compute Extension](https://github.com/Azure/cirrus/tree/main/src/Extensions/Compute) 
* Azure APIs have swagger and TypeSpec as sources, to make it easier for LLM, use swagger as spec

# Create a new Extension Project
* Create a new directory `Cirrus/src/Extensions/{ExtensionName}`
* Create a new file `Cirrus/src/Extensions/{ExtensionsName}/Extensions.{ExtensionName}.csproj`
* Add `Cirrus/src/Extensions/{ExtensionsName}/Extensions.{ExtensionName}.csproj` to `Cirrus/cirrus.slnx`
* Content 
```
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <AssemblyName>Microsoft.Azure.Cirrus.Extensions.{ExtensionName}</AssemblyName>
        <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
        <RootNamespace>Microsoft.Azure.Cirrus.Extensions.{ExtensionName}</RootNamespace>
        <ExtName>Microsoft.Azure.Cirrus.Extensions.{ExtensionName}</ExtName>

        <Description>{ExtensionName} Extension for Cirrus CLI</Description>
        <PackageLicenseExpression>MIT</PackageLicenseExpression>
        <RepositoryUrl>https://github.com/Azure/cirrus</RepositoryUrl>

        <PackageId>Microsoft.Azure.Cirrus.Extensions.{ExtensionName}</PackageId>
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
* Create directory `Cirrus/src/Extensions/{ExtensionName}/Models`
* Locate the API you are trying to support, find the schema of request/response, for example:
    * Locate API for [`POST: "/subscriptions/{subscriptionId}/providers/Microsoft.AzureTerraform/exportTerraform"`](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/terraform/resource-manager/Microsoft.AzureTerraform/preview/2025-09-01-preview/export.json#L89)
    * Find schema names for [request schema Id](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/terraform/resource-manager/Microsoft.AzureTerraform/preview/2025-09-01-preview/export.json#L110) and [response schema Id](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/terraform/resource-manager/Microsoft.AzureTerraform/preview/2025-09-01-preview/export.json#L118)
    * Find schemas by schema Ids: [request schema](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/terraform/resource-manager/Microsoft.AzureTerraform/preview/2025-09-01-preview/export.json#L191), [response schema](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/terraform/resource-manager/Microsoft.AzureTerraform/preview/2025-09-01-preview/export.json#L410)
* Ask LLM to create model classes for request schema/response schema under `Cirrus/src/Extensions/{ExtensionName}/Models`, take `Cirrus/src/Extensions/Compute` as reference
* Ask LLM to resolve and create model classes for all dependency schemas under `Cirrus/src/Extensions/{ExtensionName}/Models`

# Develop [Processors](#processors)
* Create directory `Cirrus/src/Extensions/{ExtensionName}/Processors`
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
* Ask LLM to create `Cirrus/src/Extensions/{ExtensionName}/Processors/{ExtensionName}ProcessorBase.cs`, take `Cirrus/src/Extensions/Compute` as reference
* Ask LLM to create processors for APIs you are trying to support, take `Cirrus/src/Extensions/Compute` as reference, ensure public properties match the request model class
    * Ensure generic type is the response type of the API
    * For Delete API, the generic type should be object
    * For long running operations (PUT/PATCH/POST/DELETE), ensure LRO properties are correctly set, see https://github.com/Azure/cirrus/blob/c606d4ea85b60eb0499811faa5b1310422fd9c5d/src/Extensions/Compute/Processors/ComputeProcessorBase.cs#L77-L85
    * Ensure [`CreateRequestMessage`](#createrequestmessage) matches the API
        * For APIs with request body (PUT/PATCH/POST), ensure [`CreateRequestMessage`](#createrequestmessage) wire public properties to request correctly

# Develop Orchestrators
* Create directory `Cirrus/src/Extensions/{ExtensionName}/Orchestrators`
* Identify APIs to be orchestrated, for example minimal creation for `vm` requires `vnet`, `nsg`, `nic` from Network RP
* Create processors for APIs, see [develop processors](#develop-processors)

## Orchestrators with Multi-API
* Create `Cirrus/src/Extensions/{ExtensionName}/Orchestrators/(APIName)Processor_Orchestrator.cs`
* Generic type is the model class of the final API
* Recurse [Multi-API Orchestrator](#orchestrators-with-multi-api) for required orchestrators if needed `Cirrus/src/Extensions/{ExtensionName}/Orchestrators/(RequiredOrchestratorName)Processor_Orchestrator.cs`, for example nic creation is multi-API orchestrator
* Ask LLM to compose public properties, should be union of all APIs (processors) in this orchestrator
* Override [SendAsync()](#sendasync)
    * Use `CreateExtendedProcessor<T>` to create orchestrated processors
        * `T` is the type of orchestrated processor
        * Map public properties from orchestrator to processors
    * Execute [`SendAsync()`](#sendasync) of each processors, sequentially or in parallel, except the processor for the generic type of orchestrator
    * `return await {FinalProcessor}.SendAsync();`
    * You can ask LLM to do it but I suggest try with bare hands.

## Orchestrators with no API calls
* Create `Cirrus/src/Extensions/{ExtensionName}/Orchestrators/(ArbitraryName)Processor_Orchestrator.cs`
* Generic type is `object`
* Arbitrarily compose public properties
* Override [ProcessAsync()](#processasync), orchestrate arbitrarily
* For example, an orchestrator to connect ACR
    * public properties: ACREndpoint + UserName + Password
    * `ProcessAsync`: `docker login {ACREndpoint} -u {UserName} -p {Password}`

## Orchestrators with computation
* Imagine API `A` requires property `P`, value of `P` can only be retrieved at runtime from DB by string `k`
* Let's define a computation `P = Func(K)`
* Create `Cirrus/src/Extensions/{ExtensionName}/Orchestrators/(A)Processor_Orchestrator.cs`
* Generic type is the model class of `A`
* Copy entire content from processor A to orchestrator A
* replace public property `P` with `K`
* In [CreateRequestMessage()](#createrequestmessage), assign `P = Func(k);`

## Miscellaneous
Improvise overriding [CreateRequestMessage()](#createrequestmessage), [SendAsync()](#sendasync) and [ProcessAsync()](#processasync)

# Develop ExtensionIndex
* Ask LLM to create `Cirrus/src/Extensions/{ExtensionName}/ExtensionIndex.cs`, by reviewing what you already have in `Cirrus/src/Extensions/{ExtensionName}` and `Cirrus/src/Extensions/Compute` as reference
* Review `s_psTable`, `s_cliTable`, delete entries you don't want to expose

# Register CLI Command Group
For CLI, map command group name to extension name in: `Cirrus/src/CLI/Parser/CommandGroupIndex.cs`

# Develop PowerShell Module
Ask LLM to review `Cirrus/src/Extensions/{ExtensionName}/ExtensionIndex.cs` and create a PS module under `Cirrus/src/PS`, take `Cirrus/src/PS/Compute` as reference

# Manual Verification
* Go to root of `Cirrus`
* `dotnet build cirrus.slnx`
* For CLI, `Cirrus\artifacts\Debug\Core.Cli\az.exe {CommandGroupName} {Command} ...`
* For PowerShell
    * `Import-module Cirrus\src\PS\Login\login.psd1`
    * `Import-module Cirrus\src\PS\{ModuleName}\{ModuleName}.psd1`
    * Try run PS cmdlets

# Add Test Scripts
Persist manual verification scripts in
* `Cirrus/tests/Scripts/CLI`
* `Cirrus/tests/Scripts/PS`
* No need to guarantee the script files runnable, guarantee content of scripts are runnable