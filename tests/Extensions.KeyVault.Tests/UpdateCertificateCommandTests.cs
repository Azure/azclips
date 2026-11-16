using System.Text.Json.Nodes;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using Moq;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class UpdateCertificateCommandTests
{
    private const string DateMessage = "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.";
    private const string PolicyMessage = "Policy must be a JSON object using supported classic CLI or REST policy fields without conflicting values.";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // exception also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = CreatePipelineMock();

    [Theory]
    [InlineData(null)]
    [InlineData("version1")]
    public async Task ProcessAsync_PolicyOnly_SendsPolicyPatchThenCertificateGet(string? version)
    {
        var command = CreatePolicyUpdateCommand(withAttributes: false, version: version);
        _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal([HttpMethod.Patch, HttpMethod.Get], _mockPipeline.SentRequests().Select(r => r.Method));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("version1")]
    public async Task ProcessAsync_PolicyWithAttributes_SendsPolicyPatchThenCertificatePatch(string? version)
    {
        var command = CreatePolicyUpdateCommand(withAttributes: true, version: version);
        _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal([HttpMethod.Patch, HttpMethod.Patch], _mockPipeline.SentRequests().Select(r => r.Method));
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "version1")]
    [InlineData(true, "version1")]
    public async Task ProcessAsync_Policy_SendsFirstRequestToPolicyEndpoint(bool withAttributes, string? version)
    {
        var command = CreatePolicyUpdateCommand(withAttributes: withAttributes, version: version);
        _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.EndsWith("/certificates/test/policy?api-version=2025-07-01", _mockPipeline.SentRequests()[0].RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "version1")]
    [InlineData(true, "version1")]
    public async Task ProcessAsync_Policy_SendsPolicyFieldsInPolicyRequest(bool withAttributes, string? version)
    {
        var command = CreatePolicyUpdateCommand(withAttributes: withAttributes, version: version);
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal(12, JsonNode.Parse(sentBodies[0]!)!["x509_props"]!["validity_months"]!.GetValue<int>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("version1")]
    public async Task ProcessAsync_PolicyWithAttributes_SendsAttributesRequestToCertificateVersion(string? version)
    {
        var command = CreatePolicyUpdateCommand(withAttributes: true, version: version);
        _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.EndsWith($"/certificates/test/{version}?api-version=2025-07-01", _mockPipeline.SentRequests()[1].RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("version1")]
    public async Task ProcessAsync_PolicyWithAttributes_SendsAttributesAndTagsWithoutPolicy(string? version)
    {
        var command = CreatePolicyUpdateCommand(withAttributes: true, version: version);
        List<string?> sentBodies = _mockPipeline.RespondWith("{}");
        Initialize(command, _mockPipeline);

        await command.ProcessAsync();

        Assert.Equal((false, false, 0), AttributesRequestSummary(sentBodies[1]));
    }

    [Fact]
    public async Task ProcessAsync_UnknownPolicyField_ThrowsArgumentException()
    {
        var command = new UpdateCertificateCommand { CertificateName = "test", Policy = "{\"typo\":true}" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(PolicyMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_NonObjectPolicy_ThrowsArgumentException()
    {
        var command = new UpdateCertificateCommand { CertificateName = "test", Policy = "[]" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(PolicyMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateName_ThrowsArgumentException()
    {
        var command = new UpdateCertificateCommand { CertificateName = "bad_name", Enabled = "true" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Certificate names must contain 1-127 letters, digits or hyphens.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidCertificateVersion_ThrowsArgumentException()
    {
        var command = new UpdateCertificateCommand { CertificateName = "test", CertificateVersion = "bad-version", Enabled = "true" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Certificate version must be an alphanumeric version identifier.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidEnabled_ThrowsArgumentException()
    {
        var command = new UpdateCertificateCommand { CertificateName = "test", Enabled = "maybe" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Enabled must be true or false.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidExpires_ThrowsArgumentException()
    {
        var command = new UpdateCertificateCommand { CertificateName = "test", Expires = "not-a-date" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(DateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidNotBefore_ThrowsArgumentException()
    {
        var command = new UpdateCertificateCommand { CertificateName = "test", NotBefore = "not-a-date" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal(DateMessage, Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_TagWithoutValue_ThrowsArgumentException()
    {
        var command = new UpdateCertificateCommand { CertificateName = "test", TagsInput = ["novalue"] };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Tags must use NAME=VALUE syntax.", Assert.IsType<ArgumentException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_NoChanges_ThrowsArgumentException()
    {
        var command = new UpdateCertificateCommand { CertificateName = "test" };
        Initialize(command, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(command.ProcessAsync);

        Assert.Equal("Specify at least one certificate attribute, tag or policy update.", Assert.IsType<ArgumentException>(exception).Message);
    }

    private UpdateCertificateCommand CreatePolicyUpdateCommand(bool withAttributes, string? version)
    {
        var command = new UpdateCertificateCommand
        {
            CertificateName = "test",
            CertificateVersion = version,
            Policy = "{\"x509_props\":{\"validity_months\":12}}"
        };
        if (withAttributes)
        {
            command.CertificateAttributesEnabled = false;
            command.Tags = new Dictionary<string, string>();
        }
        return command;
    }

    private (bool Enabled, bool HasPolicy, int TagCount) AttributesRequestSummary(string? sentBody)
    {
        JsonNode body = JsonNode.Parse(sentBody!)!;
        return (body["attributes"]!["enabled"]!.GetValue<bool>(), body.AsObject().ContainsKey("policy"), body["tags"]!.AsObject().Count);
    }
}