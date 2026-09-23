using System.Text.Json;
using Ace.Generator.Model.Ir;
using Xunit;

namespace Ace.Generator.Tests;

/// <summary>
/// Verifies the generated POCOs deserialize the IR's discriminated unions into the
/// right derived types via System.Text.Json polymorphism — the fidelity the
/// Option A schema refactor + Roslyn mapper exist to provide.
/// </summary>
public sealed class IrModelDeserializationTests
{
    private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, IrJson.Options)!;

    [Fact]
    public void IROperation_RoundTripsSourceInterfaceAndParameterIdentity()
    {
        IROperation operation = Parse<IROperation>(
            """
            {
              "id": { "module": "M", "namespace": ["Contoso", "Inventory"], "interface": "Widgets", "name": "read" },
              "name": "fetch",
              "server": { "module": "M", "namespace": ["Contoso", "Inventory"], "name": "server" },
              "pathParameters": [
                {
                  "id": { "module": "M", "namespace": ["Contoso", "Inventory"], "interface": "Widgets", "name": "read.widgetId" },
                  "name": "widgetId",
                  "wireName": "widgetId",
                  "type": { "kind": "primitive", "name": "string" }
                }
              ]
            }
            """);

        IROperation roundTripped = Parse<IROperation>(JsonSerializer.Serialize(operation, IrJson.Options));

        Assert.Equal("Widgets", roundTripped.Id.Interface);
        Assert.Equal(new[] { "Contoso", "Inventory" }, roundTripped.Id.Namespace);
        Assert.Equal("read", roundTripped.Id.Name);
        Assert.Equal("fetch", roundTripped.Name);
        Assert.Equal("server", roundTripped.Server.Name);
        IRParameter parameter = Assert.Single(roundTripped.PathParameters);
        Assert.Equal(roundTripped.Id.Interface, parameter.Id.Interface);
        Assert.Equal(roundTripped.Id.Namespace, parameter.Id.Namespace);
        Assert.Equal("read.widgetId", parameter.Id.Name);
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(roundTripped.Id, IrJson.Options));
        Assert.Equal("Widgets", json.RootElement.GetProperty("interface").GetString());
    }

    [Fact]
    public void PowerShellDialects_RoundTripAttributeMetadata()
    {
        PsCommandDialect command = Parse<PsCommandDialect>(
            """
            {
              "verb": "Remove",
              "noun": "AzWidget",
              "defaultParameterSet": "ByName",
              "supportsShouldProcess": true,
              "confirmImpact": "High",
              "examples": ["Remove-AzWidget -Name example"],
              "lifecycleTag": "GA",
              "helpUri": "https://learn.microsoft.com/powershell/module/az.widgets/remove-azwidget"
            }
            """);

        PsCommandDialect commandRoundTrip = Parse<PsCommandDialect>(
            JsonSerializer.Serialize(command, IrJson.Options));

        Assert.Equal(PsCommandDialectConfirmImpact.High, commandRoundTrip.ConfirmImpact);
        Assert.Equal(PsCommandDialectLifecycleTag.GA, commandRoundTrip.LifecycleTag);
        Assert.Equal("ByName", commandRoundTrip.DefaultParameterSet);
        Assert.Equal("Remove-AzWidget -Name example", Assert.Single(commandRoundTrip.Examples!));

        PsParameterDialect parameter = Parse<PsParameterDialect>(
            """
            {
              "name": "ResourceGroupName",
              "aliases": ["ResourceGroup"],
              "parameterSets": [
                { "set": "ByName", "mandatory": true, "position": 0, "valueFromPipeline": true },
                { "set": "ById", "mandatory": false, "valueFromPipelineByPropertyName": true }
              ],
              "completer": { "name": "ResourceGroupCompleter" },
              "defaultValue": {
                "script": "(Get-AzContext).Subscription.Id",
                "setCondition": "$PSBoundParameters.ContainsKey('SubscriptionId')"
              }
            }
            """);

        PsParameterDialect parameterRoundTrip = Parse<PsParameterDialect>(
            JsonSerializer.Serialize(parameter, IrJson.Options));

        Assert.Equal("ResourceGroup", Assert.Single(parameterRoundTrip.Aliases!));
        Assert.Equal(2, parameterRoundTrip.ParameterSets!.Count);
        Assert.True(parameterRoundTrip.ParameterSets[0].ValueFromPipeline);
        Assert.True(parameterRoundTrip.ParameterSets[1].ValueFromPipelineByPropertyName);
        Assert.Equal("ResourceGroupCompleter", parameterRoundTrip.Completer!.Name);
        Assert.Contains("Subscription.Id", parameterRoundTrip.DefaultValue!.Script);
    }

    [Fact]
    public void PowerShellIdentity_RoundTripsAsSharedTypeReferencedBySyntheticParameter()
    {
        TransformedIR document = Parse<TransformedIR>(
            """
            {
              "commands": {
                "sharedTypes": [
                  {
                    "kind": "identity",
                    "id": { "module": "Widgets", "namespace": [], "name": "WidgetIdentity" },
                    "name": "WidgetIdentity",
                    "properties": [
                      {
                        "name": "ResourceGroupName",
                        "sourceParameters": [
                          { "module": "Widgets", "namespace": [], "name": "get.resourceGroupName" }
                        ]
                      }
                    ]
                  }
                ],
                "items": [{
                  "parameters": [
                    {
                      "name": "InputObject",
                      "source": {
                        "kind": "synthetic",
                        "purpose": "identity",
                        "type": { "module": "Widgets", "namespace": [], "name": "WidgetIdentity" },
                        "parameters": [
                          { "module": "Widgets", "namespace": [], "name": "get.resourceGroupName" }
                        ]
                      },
                      "type": {
                        "kind": "model",
                        "id": { "module": "Widgets", "namespace": [], "name": "WidgetIdentity" }
                      },
                      "required": true,
                      "nullable": false
                    }
                  ]
                }]
              }
            }
            """);

        TransformedIR roundTripped = Parse<TransformedIR>(
            JsonSerializer.Serialize(document, IrJson.Options));

        IRCommandIdentity identity = Assert.Single(roundTripped.Commands.SharedTypes);
        Assert.Equal("WidgetIdentity", identity.Name);
        Assert.Equal("ResourceGroupName", Assert.Single(identity.Properties).Name);

        IRCommandParameter parameter = Assert.Single(Assert.Single(roundTripped.Commands.Items).Parameters);
        var source = Assert.IsType<IRCommandParameterSourceSynthetic>(parameter.Source);
        Assert.Equal(identity.Id.Name, source.Type.Name);
        Assert.Equal("get.resourceGroupName", Assert.Single(source.Parameters).Name);
    }

    [Fact]
    public void IRId_AbsentInterface_IsNotInferredFromNamespace()
    {
        IRId id = Parse<IRId>(
            """{ "module": "M", "namespace": ["Contoso", "Inventory"], "name": "get" }""");

        Assert.Null(id.Interface);
        Assert.Equal(new[] { "Contoso", "Inventory" }, id.Namespace);
    }

    [Fact]
    public void Shared_Server_References_And_Nested_Endpoint_Union_RoundTrip()
    {
        TransformedIR document = Parse<TransformedIR>(
            """
            {
              "schemaVersion": "0.8.0",
              "sources": [
                { "id": "M", "server": { "module": "M", "namespace": ["Contoso"], "name": "server" } }
              ],
              "servers": [
                {
                  "id": { "module": "M", "namespace": ["Contoso"], "name": "server" },
                  "endpoint": {
                    "servers": [
                      { "url": "https://first.example.com", "kind": "literal" },
                      {
                        "template": "https://{region}.example.com",
                        "parameters": [
                          {
                            "name": "region",
                            "wireName": "region",
                            "type": { "kind": "primitive", "name": "string" },
                            "defaultValue": { "kind": "string", "value": "west" }
                          }
                        ],
                        "kind": "parameterized"
                      },
                      { "servers": [{ "kind": "literal", "url": "https://nested.example.com" }], "kind": "oneOf" }
                    ],
                    "kind": "oneOf"
                  }
                }
              ],
              "operations": [
                { "name": "get", "server": { "module": "M", "namespace": ["Contoso"], "name": "server" } },
                { "name": "list", "server": { "module": "M", "namespace": ["Contoso"], "name": "server" } }
              ]
            }
            """);

        TransformedIR roundTripped = Parse<TransformedIR>(JsonSerializer.Serialize(document, IrJson.Options));

        IRServerDefinition definition = Assert.Single(roundTripped.Servers);
        IRId sourceReference = Assert.Single(roundTripped.Sources).Server;
        Assert.Equal(definition.Id.Module, sourceReference.Module);
        Assert.Equal(definition.Id.Namespace, sourceReference.Namespace);
        Assert.Equal(definition.Id.Name, sourceReference.Name);
        Assert.Equal(2, roundTripped.Operations.Count);
        Assert.All(roundTripped.Operations, operation =>
        {
            Assert.Equal(sourceReference.Module, operation.Server.Module);
            Assert.Equal(sourceReference.Namespace, operation.Server.Namespace);
            Assert.Equal(sourceReference.Name, operation.Server.Name);
        });
        var endpoints = Assert.IsType<IRServerOneOf>(definition.Endpoint);
        Assert.Equal(3, endpoints.Servers.Count);
        Assert.Equal("https://first.example.com", Assert.IsType<IRServerLiteral>(endpoints.Servers[0]).Url);
        var parameterized = Assert.IsType<IRServerParameterized>(endpoints.Servers[1]);
        Assert.Equal("https://{region}.example.com", parameterized.Template);
        IRServerParameter parameter = Assert.Single(parameterized.Parameters);
        Assert.Equal("region", parameter.Name);
        Assert.Equal("region", parameter.WireName);
        Assert.Equal(IRTypeRefPrimitiveName.String, Assert.IsType<IRTypeRefPrimitive>(parameter.Type).Name);
        Assert.Equal("west", Assert.IsType<IRLiteralValueString>(parameter.DefaultValue).Value);
        var nested = Assert.IsType<IRServerOneOf>(endpoints.Servers[2]);
        Assert.Equal("https://nested.example.com", Assert.IsType<IRServerLiteral>(Assert.Single(nested.Servers)).Url);
    }

    [Fact]
    public void IRTypeRef_Primitive_DeserializesToDerivedType()
    {
        IRTypeRef result = Parse<IRTypeRef>("""{ "kind": "primitive", "name": "int64" }""");

        var primitive = Assert.IsType<IRTypeRefPrimitive>(result);
        Assert.Equal(IRTypeRefPrimitiveName.Int64, primitive.Name);
    }

    [Fact]
    public void IRTypeRef_Array_NestsPolymorphicElement()
    {
        IRTypeRef result = Parse<IRTypeRef>(
            """{ "kind": "array", "element": { "kind": "model", "id": { "module": "M", "namespace": [], "name": "Widget" } } }""");

        var array = Assert.IsType<IRTypeRefArray>(result);
        var model = Assert.IsType<IRTypeRefModel>(array.Element);
        Assert.Equal("Widget", model.Id.Name);
    }

    [Fact]
    public void IRProperty_DiscriminatesByTypeKind()
    {
        IRProperty result = Parse<IRProperty>(
            """
            {
              "typeKind": "primitive",
              "id": { "module": "M", "namespace": [], "name": "Widget.size" },
              "name": "Size",
              "wireName": "size",
              "optional": false,
              "nullable": false,
              "type": { "kind": "primitive", "name": "int32" }
            }
            """);

        var primitive = Assert.IsType<IRPropertyPrimitive>(result);
        Assert.Equal("Size", primitive.Name);
        Assert.Equal("size", primitive.WireName);
        Assert.Equal(IRTypeRefPrimitiveName.Int32, primitive.Type.Name);
    }

    [Fact]
    public void IRStatusCode_And_IRLiteralValue_Discriminate()
    {
        var status = Assert.IsType<IRStatusCodeExact>(Parse<IRStatusCode>("""{ "kind": "exact", "code": 200 }"""));
        Assert.Equal(200, status.Code);

        Assert.IsType<IRLiteralValueString>(Parse<IRLiteralValue>("""{ "kind": "string", "value": "hi" }"""));
        Assert.IsType<IRLiteralValueNull>(Parse<IRLiteralValue>("""{ "kind": "null" }"""));
    }

    [Fact]
    public void IROperationSemantics_ContainsIndependentTraits()
    {
        IROperationSemantics result = Parse<IROperationSemantics>(
            """{ "longRunning": { "pollingStrategy": "location", "finalStateVia": "location" } }""");

        var lro = Assert.IsType<IRLongRunningSemantics>(result.LongRunning);
        Assert.Equal(IRLongRunningSemanticsPollingStrategy.Location, lro.PollingStrategy);
        Assert.Null(result.Pageable);
    }

    [Fact]
    public void EnumMember_WithHyphen_MapsToWireValue()
    {
        // FormatKinds carry hyphens on the wire ("date-time") but sanitized C# names.
        IRTypeRef result = Parse<IRTypeRef>("""{ "kind": "primitive", "name": "string", "format": "date-time" }""");

        var primitive = Assert.IsType<IRTypeRefPrimitive>(result);
        Assert.Equal(IRTypeRefPrimitiveFormat.DateTime, primitive.Format);
    }
}
