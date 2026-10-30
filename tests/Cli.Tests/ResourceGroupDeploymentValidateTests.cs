// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline processor tests for deployment validate (Test-AzResourceGroupDeployment / az deployment group validate): the request sent, response
/// handling, errors and output, against a fake HTTP pipeline.
/// </summary>
public sealed class ResourceGroupDeploymentValidateTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    public void Dispose() => _tempFiles.ForEach(File.Delete);

    private string TempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"azclips-validate-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private static JsonElement SentProperties(List<Sent> sent) =>
        JsonDocument.Parse(Assert.Single(sent).Body!).RootElement.GetProperty("properties").Clone();

    private static bool IsAbsentOrNull(JsonElement element, string name) =>
        !element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null;

    private static TestResourceGroupDeploymentProcessor_Orchestrator Validate(
        FakeHttpPipeline pipeline, FakeOutputHandler output, Action<TestResourceGroupDeploymentProcessor_Orchestrator>? configure = null) =>
        Configure(new TestResourceGroupDeploymentProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = "test-rg";
            p.Name = "dep";
            p.TemplateUri = "https://example.com/t.json";
            p.Parameter = ["p1=v1", "count=3"];
            p.Mode = "Complete";
            p.RollbackToDeploymentName = "previous";
            configure?.Invoke(p);
        });

    [Fact]
    public async Task Validate_PostsSerializedBody_AndWritesValidateResult()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK,
            """{"id":"/x/dep","name":"dep","properties":{"provisioningState":"Succeeded","mode":"Complete"}}"""));

        await Validate(pipeline, output).ProcessAsync();

        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(DeploymentUri + "/validate?api-version=2025-04-01", request.Uri);

        var properties = JsonDocument.Parse(request.Body!).RootElement.GetProperty("properties");
        Assert.Equal("Complete", properties.GetProperty("mode").GetString());
        Assert.Equal("https://example.com/t.json", properties.GetProperty("templateLink").GetProperty("uri").GetString());
        Assert.Equal("v1", properties.GetProperty("parameters").GetProperty("p1").GetProperty("value").GetString());
        Assert.Equal(3, properties.GetProperty("parameters").GetProperty("count").GetProperty("value").GetInt32());
        var onError = properties.GetProperty("onErrorDeployment");
        Assert.Equal("SpecificDeployment", onError.GetProperty("type").GetString());
        Assert.Equal("previous", onError.GetProperty("deploymentName").GetString());

        var result = Assert.IsType<DeploymentValidateResult>(Assert.Single(output.WrittenValues));
        Assert.Null(result.Error);
        Assert.Equal("Succeeded", result.Properties!.ProvisioningState);
    }

    [Fact]
    public async Task Validate_ArmValidationError400_ThrowsFlattenedMessage_AndWritesNothing()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.BadRequest, """
            {"error":{"code":"InvalidTemplate","message":"Template is invalid.","target":"resources[0]",
              "details":[{"code":"InvalidParameter","message":"Parameter 'p1' is not allowed."}]}}
            """));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Validate(pipeline, output).ProcessAsync());

        Assert.Single(sent);
        Assert.StartsWith("Deployment validation failed.", error.Message);
        Assert.Contains("InvalidTemplate: Template is invalid. (target: resources[0])", error.Message);
        Assert.Contains("InvalidParameter: Parameter 'p1' is not allowed.", error.Message);
        Assert.DoesNotContain("{", error.Message);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Validate_BadRequestWithoutError_ThrowsHttpError_AndWritesNothing()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.BadRequest, "{}"));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Validate(pipeline, output).ProcessAsync());

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    public async Task Validate_BadRequestWithoutVerdict_ThrowsHttpError_AndWritesNothing(string body)
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.BadRequest, body));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Validate(pipeline, output).ProcessAsync());

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Validate_InvalidJsonBody_ThrowsWithRawBody_AndWritesNothing()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.OK, "not-json"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Validate(pipeline, output).ProcessAsync());

        Assert.Contains("Failed to deserialize", error.Message);
        Assert.Contains("not-json", error.Message);
        Assert.IsAssignableFrom<JsonException>(error.InnerException);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Validate_AcceptedWithoutLocation_Throws()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.Accepted));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Validate(pipeline, output).ProcessAsync());

        Assert.Contains("Location", error.Message);
    }

    [Fact]
    public async Task Validate_NonValidationArmError_ThrowsHttpError()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.Forbidden, """{"error":{"code":"AuthorizationFailed","message":"denied"}}"""));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Validate(pipeline, output).ProcessAsync());

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task Validate_BothRollbackOptions_ThrowsBeforeSending()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsAsync<ArgumentException>(() => Validate(pipeline, output, p => p.RollbackOnError = true).ProcessAsync());

        Assert.Equal(0, pipeline.SendCount);
    }

    private static readonly Func<HttpRequestMessage, HttpResponseMessage> s_ok =
        _ => Status(HttpStatusCode.OK, """{"properties":{"provisioningState":"Succeeded"}}""");

    [Fact]
    public async Task Validate_InlineParameterForms_MergeIntoBody()
    {
        var atFile = TempFile("""{"parameters":{"fromFile":{"value":"f"}}}""");
        var (pipeline, output, sent) = Fake(s_ok);

        await Validate(pipeline, output, p => p.Parameter =
        [
            "{}",
            "k=foo=bar",
            """{"obj":{"value":"o"}}""",
            "@" + atFile,
            "n=3",
            "b=true",
            "arr=[1,2]",
            "dup=first",
            "dup=second",
            "Case=one",
            "case=two",
        ]).ProcessAsync();

        var parameters = SentProperties(sent).GetProperty("parameters");
        Assert.Equal("foo=bar", parameters.GetProperty("k").GetProperty("value").GetString());
        Assert.Equal("o", parameters.GetProperty("obj").GetProperty("value").GetString());
        Assert.Equal("f", parameters.GetProperty("fromFile").GetProperty("value").GetString());
        Assert.Equal(3, parameters.GetProperty("n").GetProperty("value").GetInt32());
        Assert.True(parameters.GetProperty("b").GetProperty("value").GetBoolean());
        Assert.Equal(2, parameters.GetProperty("arr").GetProperty("value").GetArrayLength());
        Assert.Equal("second", parameters.GetProperty("dup").GetProperty("value").GetString());
        var caseKeys = parameters.EnumerateObject().Where(p => p.Name.Equals("case", StringComparison.OrdinalIgnoreCase)).ToList();
        var caseKey = Assert.Single(caseKeys);
        Assert.Equal("Case", caseKey.Name);
        Assert.Equal("two", caseKey.Value.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Validate_InlineParameter_OverridesParametersFile()
    {
        var file = TempFile("""{"parameters":{"p1":{"value":"file"},"keep":{"value":"k"}}}""");
        var (pipeline, output, sent) = Fake(s_ok);

        await Validate(pipeline, output, p =>
        {
            p.ParametersFile = file;
            p.Parameter = ["P1=inline"];
        }).ProcessAsync();

        var parameters = SentProperties(sent).GetProperty("parameters");
        Assert.Equal("inline", parameters.GetProperty("p1").GetProperty("value").GetString());
        Assert.False(parameters.TryGetProperty("P1", out _));
        Assert.Equal("k", parameters.GetProperty("keep").GetProperty("value").GetString());
    }

    private const string MissingValuesTemplate = """
        {
          "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
          "contentVersion": "1.0.0.0",
          "parameters": {
            "zeta": { "type": "string" },
            "alpha": { "type": "string" },
            "withDefault": { "type": "string", "defaultValue": "d" },
            "optional": { "type": "string", "nullable": true },
            "secret": { "type": "securestring" }
          },
          "resources": []
        }
        """;

    private const string KeyVaultReferenceParameters = """
        {"parameters":{"secret":{"reference":{"keyVault":{"id":"/subscriptions/x/resourceGroups/rg/providers/Microsoft.KeyVault/vaults/kv"},"secretName":"s"}}}}
        """;

    [Fact]
    public async Task Validate_MissingTemplateParameters_ListedSorted_DefaultNullableAndReferenceExcluded_NoRequest()
    {
        var template = TempFile(MissingValuesTemplate);
        var parameters = TempFile(KeyVaultReferenceParameters);
        var (pipeline, output, _) = Fake(s_ok);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Validate(pipeline, output, p =>
        {
            p.TemplateUri = null;
            p.TemplateFile = template;
            p.ParametersFile = parameters;
            p.Parameter = null;
        }).ProcessAsync());

        Assert.Contains("parameter(s): alpha, zeta.", error.Message);
        Assert.DoesNotContain("withDefault", error.Message);
        Assert.DoesNotContain("optional", error.Message);
        Assert.DoesNotContain("secret", error.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task Validate_AllRequiredParametersSupplied_SendsKeyVaultReferenceUnchanged()
    {
        var template = TempFile(MissingValuesTemplate);
        var parameters = TempFile(KeyVaultReferenceParameters);
        var (pipeline, output, sent) = Fake(s_ok);

        await Validate(pipeline, output, p =>
        {
            p.TemplateUri = null;
            p.TemplateFile = template;
            p.ParametersFile = parameters;
            p.Parameter = ["alpha=a", "zeta=z"];
        }).ProcessAsync();

        var body = SentProperties(sent).GetProperty("parameters");
        Assert.Equal("s", body.GetProperty("secret").GetProperty("reference").GetProperty("secretName").GetString());
        Assert.False(body.TryGetProperty("optional", out _));
    }

    [Theory]
    [InlineData("template")]
    [InlineData("parameters")]
    public async Task Validate_NonexistentFile_ThrowsFileNotFound_NoRequest(string which)
    {
        var missing = Path.Combine(Path.GetTempPath(), $"azclips-missing-{Guid.NewGuid():N}.json");
        var (pipeline, output, sent) = Fake(s_ok);

        var error = await Assert.ThrowsAsync<FileNotFoundException>(() => Validate(pipeline, output, p => SetFile(p, which, missing)).ProcessAsync());

        Assert.Equal(missing, error.FileName);
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData("template")]
    [InlineData("parameters")]
    public async Task Validate_InvalidJsonFile_ThrowsClearError_NoRequest(string which)
    {
        var broken = TempFile("{ not json");
        var (pipeline, output, sent) = Fake(s_ok);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Validate(pipeline, output, p => SetFile(p, which, broken)).ProcessAsync());

        Assert.Contains($"Failed to parse file '{broken}' as JSON", error.Message);
        Assert.IsAssignableFrom<JsonException>(error.InnerException);
        Assert.Empty(sent);
    }

    private static void SetFile(TestResourceGroupDeploymentProcessor_Orchestrator p, string which, string path)
    {
        if (which == "template")
        {
            p.TemplateUri = null;
            p.TemplateFile = path;
        }
        else
        {
            p.ParametersFile = path;
        }
    }

    [Fact]
    public async Task Validate_NoRollbackOption_SendsNoOnErrorDeployment()
    {
        var (pipeline, output, sent) = Fake(s_ok);

        await Validate(pipeline, output, p => p.RollbackToDeploymentName = null).ProcessAsync();

        Assert.True(IsAbsentOrNull(SentProperties(sent), "onErrorDeployment"));
    }

    [Fact]
    public async Task Validate_TemplateSpecId_SendsTemplateLinkId_NoInlineTemplate()
    {
        const string specId = "/subscriptions/other/resourceGroups/rg/providers/Microsoft.Resources/templateSpecs/spec/versions/v1";
        var (pipeline, output, sent) = Fake(s_ok);

        await Validate(pipeline, output, p =>
        {
            p.TemplateUri = null;
            p.TemplateSpecId = specId;
        }).ProcessAsync();

        var properties = SentProperties(sent);
        Assert.Equal(specId, properties.GetProperty("templateLink").GetProperty("id").GetString());
        Assert.True(IsAbsentOrNull(properties.GetProperty("templateLink"), "uri"));
        Assert.True(IsAbsentOrNull(properties, "template"));
    }

    [Fact]
    public async Task Validate_TemplateObject_SendsInlineTemplate_NoTemplateLink()
    {
        var (pipeline, output, sent) = Fake(s_ok);

        await Validate(pipeline, output, p =>
        {
            p.TemplateUri = null;
            p.TemplateObject = new System.Collections.Hashtable
            {
                ["contentVersion"] = "1.0.0.0",
                ["resources"] = Array.Empty<object>(),
            };
        }).ProcessAsync();

        var properties = SentProperties(sent);
        Assert.Equal("1.0.0.0", properties.GetProperty("template").GetProperty("contentVersion").GetString());
        Assert.Equal(0, properties.GetProperty("template").GetProperty("resources").GetArrayLength());
        Assert.True(IsAbsentOrNull(properties, "templateLink"));
    }

    [Theory]
    [InlineData("Provider", "Provider")]
    [InlineData(" ProviderNoRbac ", "ProviderNoRbac")]
    [InlineData(null, null)]
    public async Task Validate_ValidationLevel_PassedThrough(string? level, string? expected)
    {
        var (pipeline, output, sent) = Fake(s_ok);

        await Validate(pipeline, output, p => p.ValidationLevel = level).ProcessAsync();

        var properties = SentProperties(sent);
        if (expected is null)
        {
            Assert.True(IsAbsentOrNull(properties, "validationLevel"));
        }
        else
        {
            Assert.Equal(expected, properties.GetProperty("validationLevel").GetString());
        }
    }
}
