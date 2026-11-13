using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.KeyVault.Customization;
using static Extensions.KeyVault.Tests.KeyVaultCommandTestHelpers;

namespace Extensions.KeyVault.Tests;

public class UpdateCertificateCommandTests
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "version1")]
    [InlineData(true, "version1")]
    public async Task UpdatePolicyComposesNamedPolicyThenReadOrAttributes(bool attributes, string? version)
    {
        var command = new UpdateCertificateCommand { CertificateName = "test", CertificateVersion = version, Policy = "{\"x509_props\":{\"validity_months\":12}}" };
        if (attributes) { command.CertificateAttributesEnabled = false; command.Tags = new Dictionary<string, string>(); }
        int calls = 0;
        Initialize(command, async request =>
        {
            if (++calls == 1)
            {
                Assert.EndsWith("/certificates/test/policy?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
                Assert.Equal(HttpMethod.Patch, request.Method);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.Equal(12, body.RootElement.GetProperty("x509_props").GetProperty("validity_months").GetInt32());
            }
            else
            {
                Assert.Equal(attributes ? HttpMethod.Patch : HttpMethod.Get, request.Method);
                if (attributes)
                {
                    Assert.EndsWith($"/certificates/test/{version}?api-version=2025-07-01", request.RequestUri!.AbsoluteUri);
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                    Assert.False(body.RootElement.GetProperty("attributes").GetProperty("enabled").GetBoolean());
                    Assert.False(body.RootElement.TryGetProperty("policy", out _));
                    Assert.Empty(body.RootElement.GetProperty("tags").EnumerateObject());
                }
            }
            return await Response("{}");
        });
        await command.ProcessAsync();
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task InvalidPolicySendsNothing()
    {
        var command = new UpdateCertificateCommand { CertificateName = "test", Policy = "{\"typo\":true}" };
        Initialize(command, _ => throw new Exception("Network must not run."));
        await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());
    }

    [Fact]
    public Task Policy_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateCertificateCommand { CertificateName = "test", Policy = "[]" },
            "Policy must be a JSON object using supported classic CLI or REST policy fields without conflicting values.");

    [Fact]
    public Task CertificateName_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateCertificateCommand { CertificateName = "bad_name", Enabled = "true" },
            "Certificate names must contain 1-127 letters, digits or hyphens.");

    [Fact]
    public Task CertificateVersion_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateCertificateCommand { CertificateName = "test", CertificateVersion = "bad-version", Enabled = "true" },
            "Certificate version must be an alphanumeric version identifier.");

    [Fact]
    public Task Enabled_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateCertificateCommand { CertificateName = "test", Enabled = "maybe" },
            "Enabled must be true or false.");

    [Fact]
    public Task Expires_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateCertificateCommand { CertificateName = "test", Expires = "not-a-date" },
            "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.");

    [Fact]
    public Task NotBefore_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateCertificateCommand { CertificateName = "test", NotBefore = "not-a-date" },
            "Dates must be UTC timestamps between 1970 and January 2038 supported by the selected API.");

    [Fact]
    public Task Tags_IsValidated() =>
        AssertRejectedBeforeNetworkAsync(new UpdateCertificateCommand { CertificateName = "test", TagsInput = ["novalue"] },
            "Tags must use NAME=VALUE syntax.");

    [Fact]
    public Task NoChanges_ThrowsBeforeNetwork() =>
        AssertRejectedBeforeNetworkAsync(new UpdateCertificateCommand { CertificateName = "test" },
            "Specify at least one certificate attribute, tag or policy update.");
}
