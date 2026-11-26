// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Engine;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Moq;
using static Cmdlets.Tests.Commands.CommandTestHelpers;
using ResourcesIndex = Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex;

namespace Cmdlets.Tests.Commands.Resources;

/// <summary>
/// Runs Invoke-AzRestMethod in PowerShell through the real engine and checks the error for each rejected input. The
/// wire mock is strict with no responses, so each test also proves no request was sent.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class RestCommandTests : IDisposable
{

    private readonly Mock<ISendAsync> _mockWire = new(MockBehavior.Strict);
    private readonly Engine _engine;

    public RestCommandTests()
    {
        _engine = CreateEngine(typeof(ResourcesIndex).Assembly, _mockWire);
        RegisterEngine(_engine);
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        _engine.Dispose();
    }

    // ── Invoke-AzRestMethod ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Invoke_InvokeRestMethodWithoutEndpoint_ThrowsInvalidInput()
    {
        string method = "GET";
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("Method", method);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "An endpoint is required: supply either a full URL (Uri) or the piece-wise ByParameters inputs " +
            "(SubscriptionId/ResourceGroupName/ResourceProviderName/ResourceType/Name/ApiVersion) to build an ARM URL.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // The cmdlet has no parameter sets, so Uri and the piece-wise parameters bind together and reach the extension.
    [Fact]
    public void Invoke_InvokeRestMethodUriAndPieceWiseParameters_ThrowsInvalidInput()
    {
        string resourceGroupName = "rg";
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("Uri", "/subscriptions").AddParameter("ResourceGroupName", resourceGroupName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Specify the endpoint either as a full URL (Uri) OR as the piece-wise parameters " +
            "(SubscriptionId/ResourceGroupName/ResourceProviderName/ResourceType/Name/ApiVersion), not both.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    // [ValidateSet] rejects the method while binding, before the extension's own "Unsupported HTTP method" check. The
    // binder throws the internal ParameterBindingValidationException, so the type is matched by its public base.
    [Fact]
    public void Invoke_InvokeRestMethodMethodNotInSet_ThrowsParameterBindingException()
    {
        string unsupportedMethod = "TRACE";
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("Uri", "/subscriptions").AddParameter("Method", unsupportedMethod);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Cannot validate argument on parameter 'Method'. The argument \"TRACE\" does not belong to the set \"GET,POST,PUT,PATCH,DELETE,HEAD,OPTIONS\" specified by the ValidateSet attribute. Supply an argument that is in the set and then try the command again.",
            Assert.IsAssignableFrom<ParameterBindingException>(exception).Message);
    }

    [Fact]
    public void Invoke_InvokeRestMethodHttpUrl_ThrowsInvalidInput()
    {
        string httpUrl = "http://management.azure.com/subscriptions";
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("Uri", httpUrl);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Absolute URL 'http://management.azure.com/subscriptions' must use https. The rest command only targets ARM over https in this release.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_InvokeRestMethodNonArmHost_ThrowsInvalidInput()
    {
        string nonArmHostUrl = "https://graph.microsoft.com/v1.0/me";
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("Uri", nonArmHostUrl);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Host 'graph.microsoft.com' is not an allowed Azure Resource Manager endpoint. The rest command only targets ARM in this release. " +
            "Allowed hosts: management.azure.com, management.chinacloudapi.cn, management.microsoftazure.de, management.usgovcloudapi.net.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_InvokeRestMethodPieceWiseWithoutApiVersion_ThrowsInvalidInput()
    {
        string resourceGroupName = "rg";
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("ResourceGroupName", resourceGroupName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "ApiVersion is required when building an ARM URL from piece-wise parameters.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_InvokeRestMethodMoreNamesThanResourceTypes_ThrowsInvalidInput()
    {
        string[] twoNames = ["first", "second"];
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("ResourceProviderName", "Microsoft.Storage")
            .AddParameter("ResourceType", new[] { "storageAccounts" })
            .AddParameter("Name", twoNames)
            .AddParameter("ApiVersion", "2023-01-01");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Name (2) must equal ResourceType (1) or be exactly one fewer (one trailing type without a name performs a collection GET on the last type).",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_InvokeRestMethodWhitespaceResourceType_ThrowsInvalidInput()
    {
        string[] whitespaceResourceType = [" "];
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("ResourceProviderName", "Microsoft.Storage")
            .AddParameter("ResourceType", whitespaceResourceType)
            .AddParameter("ApiVersion", "2023-01-01");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "ResourceType segments must be non-empty; ResourceType element 0 was empty or whitespace.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_InvokeRestMethodWhitespaceName_ThrowsInvalidInput()
    {
        string[] whitespaceName = [" "];
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("ResourceProviderName", "Microsoft.Storage")
            .AddParameter("ResourceType", new[] { "storageAccounts" })
            .AddParameter("Name", whitespaceName)
            .AddParameter("ApiVersion", "2023-01-01");

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Name segments must be non-empty; Name element 0 was empty or whitespace.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_InvokeRestMethodUriParameterWithoutValue_ThrowsInvalidInput()
    {
        string uriParameterWithoutValue = "top";
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("Uri", "/subscriptions").AddParameter("UriParameters", uriParameterWithoutValue);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Invalid uri parameter 'top'. Expected KEY=VALUE format.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_InvokeRestMethodUriParameterWithBlankName_ThrowsInvalidInput()
    {
        string uriParameterWithBlankName = " =5";
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("Uri", "/subscriptions").AddParameter("UriParameters", uriParameterWithBlankName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Invalid uri parameter ' =5': parameter name cannot be empty.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_InvokeRestMethodHeaderWithoutValue_ThrowsInvalidInput()
    {
        string headerWithoutValue = "Accept";
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("Uri", "/subscriptions").AddParameter("Headers", headerWithoutValue);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Invalid header 'Accept'. Expected KEY=VALUE format.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }

    [Fact]
    public void Invoke_InvokeRestMethodHeaderWithBlankName_ThrowsInvalidInput()
    {
        string headerWithBlankName = " =json";
        using PowerShell shell = CreateShell<InvokeAzclipsRestMethodCmdlet>();
        shell.AddParameter("Uri", "/subscriptions").AddParameter("Headers", headerWithBlankName);

        Exception? exception = Record.Exception(() => shell.Invoke());

        Assert.Equal(
            "Invalid header ' =json': header name cannot be empty.",
            Assert.IsType<InvalidInputException>(Assert.IsType<CmdletInvocationException>(exception).InnerException).Message);
    }
}
