// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Globalization;
using Core.Abstractions.Runtime;
using Core.Cli.Parser;
using Microsoft.Azure.Azclips.Extensions.Accounts.Models;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Models;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Processors;
using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Moq;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="Core.Cli.Output.CliOutputFormatter"/> ensuring CLI output
/// correctly renders strings, primitives, complex objects, and collections.
/// </summary>
// One test redirects the process-global Console.Out to capture output end-to-end.
// Join the non-parallel "Azclips Service Provider Tests" collection so it never runs
// concurrently with engine-based tests that write log lines to Console.
[Collection("Azclips Service Provider Tests")]
public class CliOutputFormatterTests
{
    [Theory]
    [InlineData("show")]
    [InlineData("list")]
    [InlineData("delete")]
    [InlineData("create")]
    public async Task CertificateCommandsRenderClassicCliFields(string operation)
    {
        const string bundle = """{"id":"https://fixture.vault.azure.net/certificates/test/version1","x5t":"-_8","cer":"AQID","attributes":{"enabled":false,"created":1700000000,"exp":1800000000},"policy":{"issuer":{"name":"Self"},"key_props":{"kty":"RSA","key_size":2048},"x509_props":{"subject":"CN=fixture","validity_months":12},"lifetime_actions":[{"action":{"action_type":"AutoRenew"},"trigger":{"lifetime_percentage":80}}]},"recoveryId":"recovery","deletedDate":1700000000}""";
        string payload = operation == "create"
            ? """{"id":"https://fixture.vault.azure.net/certificates/test/pending","issuer":{"name":"Self"},"status":"completed","request_id":"request","status_details":"complete","cancellation_requested":false,"csr":"AQID"}"""
            : operation == "list" ? "{\"value\":[" + bundle + "]}" : bundle;
        KeyVaultDataPlaneProcessorBase command = operation switch
        {
            "show" => new ShowCertificateCommand { CertificateName = "test" },
            "list" => new ListCertificatesCommand(),
            "delete" => new RemoveCertificateCommand { CertificateName = "test", Yes = true },
            _ => new CreateCertificateCommand { CertificateName = "test", Policy = "{}" }
        };
        var pipeline = new Mock<IRuntimeHttpPipeline>();
        pipeline.Setup(value => value.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<IEventPublisher>()))
            .ReturnsAsync(() => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(payload) });
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        object? result = null;
        command.VaultName = "fixture";
        command.Init(new ProcessorContext(pipeline.Object, publisher.Object, Mock.Of<IAuthHandler>(),
            new Core.Runtime.Handler.OutputHandler { WriteOutput = (value, _) => { result = value; return Task.CompletedTask; } },
            new Profile { TenantId = "fixture" }));
        await command.ProcessAsync();
        using var formatted = JsonDocument.Parse(Core.Cli.Output.CliOutputFormatter.FormatValue(result));
        var output = operation == "list" ? formatted.RootElement[0] : formatted.RootElement;
        Assert.Equal("test", output.GetProperty("name").GetString());
        if (operation == "create")
        {
            Assert.Equal("Self", output.GetProperty("issuerParameters").GetProperty("name").GetString());
            Assert.Equal("request", output.GetProperty("requestId").GetString());
            Assert.Equal("AQID", output.GetProperty("csr").GetString());
            Assert.False(output.GetProperty("cancellationRequested").GetBoolean());
            return;
        }
        Assert.Equal("+/8=", output.GetProperty("x509Thumbprint").GetString());
        Assert.Equal("FBFF", output.GetProperty("x509ThumbprintHex").GetString());
        Assert.False(output.TryGetProperty("x5t", out _));
        if (operation == "list") Assert.Equal(string.Empty, output.GetProperty("subject").GetString());
        else Assert.False(output.TryGetProperty("subject", out _));
        var attributes = output.GetProperty("attributes");
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1800000000), attributes.GetProperty("expires").GetDateTimeOffset());
        Assert.False(attributes.TryGetProperty("exp", out _));
        if (operation != "list")
        {
            Assert.Equal("AQID", output.GetProperty("cer").GetString());
            var policy = output.GetProperty("policy");
            Assert.Equal("RSA", policy.GetProperty("keyProperties").GetProperty("keyType").GetString());
            Assert.Equal(12, policy.GetProperty("x509CertificateProperties").GetProperty("validityInMonths").GetInt32());
            Assert.Equal(80, policy.GetProperty("lifetimeActions")[0].GetProperty("trigger").GetProperty("lifetimePercentage").GetInt32());
        }
        if (operation == "delete") Assert.Equal("recovery", output.GetProperty("recoveryId").GetString());
    }

    [Theory]
    [InlineData(nameof(CertificateBundle), false)]
    [InlineData(nameof(CertificateBundle), true)]
    [InlineData(nameof(CertificateItem), false)]
    [InlineData(nameof(CertificateItem), true)]
    [InlineData(nameof(DeletedCertificateBundle), false)]
    [InlineData(nameof(DeletedCertificateBundle), true)]
    public void FormatValue_ShouldPreserveCertificateThumbprintEncoding(string model, bool collection)
    {
        byte[] bytes = [251, 255];
        object certificate = model switch
        {
            nameof(CertificateBundle) => new CertificateBundle { X509Thumbprint = bytes, Cer = bytes },
            nameof(CertificateItem) => new CertificateItem { X509Thumbprint = bytes },
            nameof(DeletedCertificateBundle) => new DeletedCertificateBundle { X509Thumbprint = bytes, Cer = bytes },
            _ => throw new ArgumentOutOfRangeException(nameof(model))
        };

        string result = Core.Cli.Output.CliOutputFormatter.FormatValue(collection ? new[] { certificate } : certificate);
        using var document = JsonDocument.Parse(result);
        var output = collection ? document.RootElement[0] : document.RootElement;

        Assert.Equal("-_8", output.GetProperty("x5t").GetString());
        if (model != nameof(CertificateItem)) Assert.Equal("+/8=", output.GetProperty("cer").GetString());
        var roundTrip = JsonSerializer.Deserialize(output.GetRawText(), certificate.GetType());
        Assert.Equal(JsonSerializer.Serialize(certificate, certificate.GetType()), JsonSerializer.Serialize(roundTrip, certificate.GetType()));
    }

    // ── String pass-through ──────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldReturnString_WhenValueIsString()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue("hello world");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void FormatValue_ShouldReturnEmptyString_WhenValueIsEmptyString()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue("");
        Assert.Equal("", result);
    }

    // ── Null handling ────────────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldReturnEmptyString_WhenValueIsNull()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(null);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FormatValue_ShouldApplyQueryToStructuredJsonValue_WithoutDoubleEncoding()
    {
        var json = "[{\"name\":\"rg1\"},{\"name\":\"rg2\"}]";
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(
            new SerializedJson(json), OutputFormat.Json, query: "[].name");

        Assert.Equal("[\"rg1\",\"rg2\"]", result);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("42")]
    public void FormatValue_ShouldPreserveOrdinaryStringType_WhenQueryIsApplied(string value)
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(value, OutputFormat.Json, query: "@");

        Assert.Equal(JsonSerializer.Serialize(value), result);
    }

    [Fact]
    public void FormatValue_ShouldRecognizeJsonNumber_IndependentOfCurrentCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            using var document = JsonDocument.Parse("1.5");

            var result = Core.Cli.Output.CliOutputFormatter.FormatValue(
                document.RootElement.Clone(), OutputFormat.Jsonc, colorPolicyAllowsColor: true, query: "@");

            Assert.Equal("1.5", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void FormatValue_ShouldApplyQueryToNullValue_WhenQueryIsPresent()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(null, OutputFormat.Json, query: "@");

        Assert.Equal("null", result);
    }

    // ── Simple vs. complex format selection (#835) ────────────────────

    [Theory]
    [InlineData("null", OutputFormat.Tsv)]
    [InlineData("\"rg1\"", OutputFormat.Tsv)]
    [InlineData("42", OutputFormat.Tsv)]
    [InlineData("true", OutputFormat.Tsv)]
    [InlineData("{\"name\":\"rg1\"}", OutputFormat.Table)]
    [InlineData("[{\"name\":\"rg1\"}]", OutputFormat.Table)]
    public void SelectFormat_ShouldChooseByJsonShape(string json, OutputFormat expected)
    {
        var result = Core.Cli.Output.CliOutputFormatter.SelectFormat(json, complexFormat: OutputFormat.Table, simpleFormat: OutputFormat.Tsv);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatValue_ShouldUseSimpleFormat_WhenQueryNarrowsResultToScalar()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(
            new[] { new { name = "rg1" } },
            complexFormat: OutputFormat.Json,
            simpleFormat: OutputFormat.Tsv,
            colorPolicyAllowsColor: false,
            query: "[0].name",
            selectedFormat: out var selectedFormat);

        Assert.Equal("rg1", result);
        Assert.Equal(OutputFormat.Tsv, selectedFormat);
    }

    [Fact]
    public void FormatValue_ShouldUseComplexFormat_WhenQueryResultIsStillAnArray()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(
            new[] { new { name = "rg1" }, new { name = "rg2" } },
            complexFormat: OutputFormat.Json,
            simpleFormat: OutputFormat.Tsv,
            colorPolicyAllowsColor: false,
            query: "[].name",
            selectedFormat: out var selectedFormat);

        Assert.Equal("[\"rg1\",\"rg2\"]", result);
        Assert.Equal(OutputFormat.Json, selectedFormat);
    }

    [Fact]
    public void FormatValue_ShouldUseComplexFormat_WhenNoQueryIsApplied()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(
            new { name = "rg1" },
            complexFormat: OutputFormat.Yaml,
            simpleFormat: OutputFormat.Tsv,
            colorPolicyAllowsColor: false,
            query: null,
            selectedFormat: out _);

        Assert.Equal("name: rg1", result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("rg1", "rg1")]
    [InlineData(42, "42")]
    [InlineData(true, "true")]
    public void FormatValue_ShouldUseSimpleFormat_ForValuesThatBypassRendering(object? value, string expected)
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(
            value,
            complexFormat: OutputFormat.Table,
            simpleFormat: OutputFormat.Tsv,
            colorPolicyAllowsColor: false,
            query: null,
            selectedFormat: out var selectedFormat);

        Assert.Equal(expected, result);
        Assert.Equal(OutputFormat.Tsv, selectedFormat);
    }

    [Theory]
    [InlineData("42", OutputFormat.Tsv)]
    [InlineData("{\"name\":\"rg1\"}", OutputFormat.Table)]
    [InlineData("[{\"name\":\"rg1\"}]", OutputFormat.Table)]
    public void FormatValue_ShouldClassifySerializedJsonWithoutRendering(
        string json,
        OutputFormat expectedFormat)
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(
            new SerializedJson(json),
            complexFormat: OutputFormat.Table,
            simpleFormat: OutputFormat.Tsv,
            colorPolicyAllowsColor: false,
            query: null,
            selectedFormat: out var selectedFormat);

        Assert.Equal(json, result);
        Assert.Equal(expectedFormat, selectedFormat);
    }

    // ── Primitives ───────────────────────────────────────────────────

    [Theory]
    [InlineData(42, "42")]
    [InlineData(3.14, "3.14")]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    [InlineData('x', "\"x\"")]
    public void FormatValue_ShouldEmitJsonLiteral_WhenValueIsPrimitive(object value, string expected)
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(value);
        Assert.Equal(expected, result);
    }

    // ── Complex objects → JSON ───────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeToJson_WhenValueIsComplexObject()
    {
        var obj = new { Name = "test-rg", Location = "eastus" };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(obj);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("test-rg", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("eastus", doc.RootElement.GetProperty("location").GetString());
    }

    // ── Format routing: table/tsv/yaml delegate to their formatters ──

    [Fact]
    public void FormatValue_ShouldRouteToTableFormatter_WhenFormatIsTable()
    {
        var arr = new[] { new { Name = "rg1" } };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(arr, OutputFormat.Table);

        Assert.Equal(Core.Cli.Output.TableFormatter.Format("""[{"name":"rg1"}]"""), result);
    }

    [Fact]
    public void FormatValue_ShouldRouteToTsvFormatter_WhenFormatIsTsv()
    {
        var arr = new[] { new { Name = "rg1" } };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(arr, OutputFormat.Tsv);

        Assert.Equal("rg1", result);
    }

    [Fact]
    public void FormatValue_ShouldRouteToYamlFormatter_WhenFormatIsYaml()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Yaml);

        Assert.Equal("name: rg1", result);
    }

    [Fact]
    public void FormatValue_ShouldRouteToYamlFormatter_WhenFormatIsYamlc()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Yamlc);

        Assert.Equal("name: rg1", result);
    }

    // ── Arrays / Collections → JSON array ────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeArray_WhenValueIsArray()
    {
        var arr = new[] { new { Name = "rg1" }, new { Name = "rg2" } };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(arr);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("rg1", doc.RootElement[0].GetProperty("name").GetString());
        Assert.Equal("rg2", doc.RootElement[1].GetProperty("name").GetString());
    }

    [Fact]
    public void FormatValue_ShouldSerializeList_WhenValueIsList()
    {
        var list = new List<string> { "alpha", "beta", "gamma" };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(list);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(3, doc.RootElement.GetArrayLength());
        Assert.Equal("alpha", doc.RootElement[0].GetString());
    }

    // ── Dictionaries → JSON object ──────────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeDictionary_WhenValueIsDictionary()
    {
        var dict = new Dictionary<string, string>
        {
            ["env"] = "prod",
            ["team"] = "platform"
        };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(dict);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        Assert.Equal("prod", doc.RootElement.GetProperty("env").GetString());
        Assert.Equal("platform", doc.RootElement.GetProperty("team").GetString());
    }

    // ── Nested structures ───────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeNestedObjects_WhenValueHasNestedProperties()
    {
        var nested = new
        {
            Name = "rg1",
            Tags = new Dictionary<string, string> { ["env"] = "test" },
            Items = new[] { 1, 2, 3 }
        };
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(nested);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("rg1", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("test", doc.RootElement.GetProperty("tags").GetProperty("env").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    // ── Decimal (non-primitive, non-string value type) ──────────────

    [Fact]
    public void FormatValue_ShouldEmitJsonLiteral_WhenValueIsDecimal()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(99.99m);
        Assert.Equal("99.99", result);
    }

    // ── Serialization errors rethrown ──────────────────────────────

    [Fact]
    public void FormatValue_ShouldThrow_WhenSerializationFails()
    {
        var unserializable = new UnserializableType();

        Assert.ThrowsAny<JsonException>(() => Core.Cli.Output.CliOutputFormatter.FormatValue(unserializable));
    }

    // ── Empty array ─────────────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldSerializeEmptyArray_WhenValueIsEmptyArray()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(Array.Empty<object>());
        Assert.Equal("[]", result);
    }

    // ── Integration: captures console output end-to-end ─────────────

    [Fact]
    public void FormatValue_ShouldWriteFormattedJsonToConsole_WhenValueIsArray()
    {
        var items = new[] { new { Id = "1", Name = "rg1" }, new { Id = "2", Name = "rg2" } };

        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            Console.WriteLine(Core.Cli.Output.CliOutputFormatter.FormatValue(items));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = sw.ToString();
        using var doc = JsonDocument.Parse(output.Trim());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    // ── Access-token JSON contract (CLI field names) ────────────────

    [Fact]
    public void FormatValue_AccessTokenResult_UsesCliJsonFieldNames()
    {
        var token = new AccessTokenResult
        {
            Token = "the-raw-token",
            ExpiresOn = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
            TenantId = "tid",
            // SubscriptionId and UserId left null on purpose to assert they are omitted.
        };

        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(token);

        using var doc = JsonDocument.Parse(result);
        // Classic `az account get-access-token` exposes accessToken, expires_on, tenant, tokenType;
        // the camel-case policy must not turn these into token / expiresOnUnixTime / tenantId.
        Assert.Equal("the-raw-token", doc.RootElement.GetProperty("accessToken").GetString());
        Assert.Equal(1_700_000_000, doc.RootElement.GetProperty("expires_on").GetInt64());
        Assert.Equal("tid", doc.RootElement.GetProperty("tenant").GetString());
        Assert.Equal("Bearer", doc.RootElement.GetProperty("tokenType").GetString());
        Assert.False(doc.RootElement.TryGetProperty("token", out _));
        Assert.False(doc.RootElement.TryGetProperty("tenantId", out _));
        Assert.False(doc.RootElement.TryGetProperty("subscriptionId", out _));
        // The deprecated local-time `expiresOn` string is not emitted; only the epoch `expires_on` remains.
        Assert.False(doc.RootElement.TryGetProperty("expiresOn", out _));
        // Nullable subscription/userId are omitted when unset (classic az omits `subscription`).
        Assert.False(doc.RootElement.TryGetProperty("subscription", out _));
        Assert.False(doc.RootElement.TryGetProperty("userId", out _));
    }

    [Fact]
    public void FormatValue_AccessTokenResult_EmitsSubscriptionAndUserId_WhenPresent()
    {
        var token = new AccessTokenResult
        {
            Token = "the-raw-token",
            ExpiresOn = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
            TenantId = "tid",
            SubscriptionId = "sub-1",
            UserId = "user@contoso.com",
        };

        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(token);

        using var doc = JsonDocument.Parse(result);
        Assert.Equal("sub-1", doc.RootElement.GetProperty("subscription").GetString());
        Assert.Equal("user@contoso.com", doc.RootElement.GetProperty("userId").GetString());
        Assert.False(doc.RootElement.TryGetProperty("subscriptionId", out _));
    }

    [Fact]
    public void FormatValue_AccessTokenResult_ExpiresOn_EmittedAsWholeSecondEpoch()
    {
        var token = new AccessTokenResult
        {
            Token = "the-raw-token",
            // Sub-second precision on purpose: classic az emits `expires_on` as whole-second epoch, so
            // the converter must truncate the fraction rather than round or leak it.
            ExpiresOn = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).AddTicks(1_512_345),
            TenantId = "tid",
        };

        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(token);

        using var doc = JsonDocument.Parse(result);
        // Classic `az account get-access-token` emits `expires_on` as an integer epoch (seconds).
        var expiresOn = doc.RootElement.GetProperty("expires_on");
        Assert.Equal(JsonValueKind.Number, expiresOn.ValueKind);
        Assert.Equal(1_700_000_000, expiresOn.GetInt64());
        // The deprecated local-time `expiresOn` string is gone.
        Assert.False(doc.RootElement.TryGetProperty("expiresOn", out _));
    }

    // ── Relaxed escaping: raw JSON strings stay human-readable ──────

    [Fact]
    public void FormatValue_ShouldNotUnicodeEscapeQuotes_WhenValueContainsRawJsonString()
    {
        // Mirrors the `rest` command's RestResponse.Content: a property whose value is itself a raw
        // JSON body. The inner quotes must render as `"` (not `\u0022`) for readable console output.
        var value = new { Content = "{\"name\":\"rg1\"}" };

        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(value);

        Assert.DoesNotContain("\\u0022", result);
        Assert.Contains("{\\\"name\\\":\\\"rg1\\\"}", result);
    }

    [Fact]
    public void FormatValue_ShouldNotUnicodeEscapeNonAscii_WhenValueContainsNonAsciiText()
    {
        var value = new { Name = "café-café" };

        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(value);

        Assert.Contains("café-café", result);
        Assert.DoesNotContain("\\u00e9", result);
    }

    // ── Colorize opt-in ──────────────────────────────────────────────

    [Fact]
    public void FormatValue_ShouldNotColorize_WhenColorizeParamOmitted()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" });

        Assert.DoesNotContain('\u001b', result);
    }

    [Fact]
    public void FormatValue_ShouldColorizeJson_WhenFormatIsJsoncAndPolicyAllowsColor()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Jsonc, colorPolicyAllowsColor: true);

        Assert.Contains('\u001b', result);
        using var doc = JsonDocument.Parse(StripAnsi(result));
        Assert.Equal("rg1", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void FormatValue_ShouldNotColorizeJson_WhenFormatIsJsonEvenIfPolicyAllowsColor()
    {
        // json (no 'c') never colorizes, regardless of core.output.color -- deterministic output
        // for scripting even if the user has core.output.color=always configured globally.
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Json, colorPolicyAllowsColor: true);

        Assert.DoesNotContain('\u001b', result);
    }

    [Fact]
    public void FormatValue_ShouldNotColorizeJsonc_WhenPolicyDisallowsColor()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Jsonc, colorPolicyAllowsColor: false);

        Assert.DoesNotContain('\u001b', result);
    }

    [Fact]
    public void FormatValue_ShouldColorizeYaml_WhenFormatIsYamlcAndPolicyAllowsColor()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Yamlc, colorPolicyAllowsColor: true);

        Assert.Contains('\u001b', result);
        Assert.Equal("name: rg1", StripAnsi(result));
    }

    [Fact]
    public void FormatValue_ShouldNotColorizeYaml_WhenFormatIsYamlEvenIfPolicyAllowsColor()
    {
        // yaml (no 'c') never colorizes, regardless of core.output.color -- same deterministic
        // guarantee json makes.
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Yaml, colorPolicyAllowsColor: true);

        Assert.DoesNotContain('\u001b', result);
    }

    [Fact]
    public void FormatValue_ShouldNotColorizeYamlc_WhenPolicyDisallowsColor()
    {
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(new { Name = "rg1" }, OutputFormat.Yamlc, colorPolicyAllowsColor: false);

        Assert.DoesNotContain('\u001b', result);
    }

    [Fact]
    public void FormatValue_ShouldNotColorizeStrings_WhenColorizeIsTrue()
    {
        // Strings render as-is (not JSON), so colorize has no effect on them.
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue("hello world", OutputFormat.Jsonc, colorPolicyAllowsColor: true);

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void FormatValue_ShouldNotColorizePrimitives_WhenColorizeIsTrue()
    {
        // Primitives/decimals are out of scope for colorizing (matches the primitive branch,
        // which renders as a single bare JSON literal with little to gain from highlighting).
        var result = Core.Cli.Output.CliOutputFormatter.FormatValue(42, OutputFormat.Jsonc, colorPolicyAllowsColor: true);

        Assert.Equal("42", result);
    }

    private static string StripAnsi(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, "\u001b\\[[0-9;]*m", string.Empty);

    /// <summary>
    /// A type that intentionally fails JSON serialization by containing a self-reference.
    /// </summary>
    private class UnserializableType
    {
        public UnserializableType Self => this;
        public override string ToString() => "UnserializableType:fallback";
    }
}
