// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Reflection;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Storage;
using Moq;

namespace Cmdlets.Tests;

public class StorageBlobCmdletTests
{
    [Theory]
    [InlineData(typeof(SetStorageBlobContentCmdlet))]
    [InlineData(typeof(GetStorageBlobContentCmdlet))]
    public void TransferCmdletsExposeAsJob(Type cmdletType)
    {
        PropertyInfo property = cmdletType.GetProperty("AsJob")!;

        Assert.Equal(typeof(SwitchParameter), property.PropertyType);
        Assert.NotEmpty(property.GetCustomAttributes<ParameterAttribute>());
    }

    [Fact]
    public void StorageTransferJobRunsEngineSession()
    {
        var session = new Mock<ISession>();
        var engine = new Mock<IEngine>();
        engine.Setup(value => value.OpenExecutionSession(
                It.IsAny<CommandInvocation>(),
                It.IsAny<OutputStreams>(),
                It.IsAny<CancellationToken>()))
            .Returns(session.Object);
        var invocation = new CommandInvocation(
            "Storage",
            "Get-AzStorageBlobContent",
            new Dictionary<string, object>(),
            false);
        Type jobType = typeof(GetStorageBlobContentCmdlet).Assembly.GetType(
            "Microsoft.Azure.Azclips.Cmdlets.Storage.StorageTransferJob",
            throwOnError: true)!;

        using var job = (Job)Activator.CreateInstance(
            jobType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            ["test", engine.Object, invocation],
            culture: null)!;

        Assert.True(SpinWait.SpinUntil(() => job.JobStateInfo.State != JobState.Running, TimeSpan.FromSeconds(5)));
        Assert.Equal(JobState.Completed, job.JobStateInfo.State);
        session.Verify(value => value.Run(), Times.Once);
    }

    [Fact]
    public void GetStorageBlobSupportsListAndGetParameterSets()
    {
        CmdletAttribute cmdlet = typeof(GetStorageBlobCmdlet).GetCustomAttribute<CmdletAttribute>()!;
        PropertyInfo blobName = typeof(GetStorageBlobCmdlet).GetProperty(nameof(GetStorageBlobCmdlet.BlobName))!;

        Assert.Equal("List", cmdlet.DefaultParameterSetName);
        Assert.Contains(
            blobName.GetCustomAttributes<ParameterAttribute>(),
            parameter => parameter.ParameterSetName == "Get" && parameter.Mandatory);
    }

    [Fact]
    public void RemoveStorageBlobSupportsConfirmationAndDeleteOptions()
    {
        CmdletAttribute cmdlet = typeof(RemoveStorageBlobCmdlet).GetCustomAttribute<CmdletAttribute>()!;
        PropertyInfo blobName = typeof(RemoveStorageBlobCmdlet).GetProperty(nameof(RemoveStorageBlobCmdlet.BlobName))!;

        Assert.True(cmdlet.SupportsShouldProcess);
        Assert.Equal(ConfirmImpact.High, cmdlet.ConfirmImpact);
        Assert.Contains(
            blobName.GetCustomAttributes<ParameterAttribute>(),
            parameter => parameter.Mandatory && parameter.Position == 0);
        Assert.Equal(
            typeof(SwitchParameter),
            typeof(RemoveStorageBlobCmdlet).GetProperty(nameof(RemoveStorageBlobCmdlet.PassThru))!.PropertyType);
    }

    [Fact]
    public void StartStorageBlobCopySupportsUriCopyAndConfirmation()
    {
        CmdletAttribute cmdlet = typeof(StartStorageBlobCopyCmdlet).GetCustomAttribute<CmdletAttribute>()!;
        PropertyInfo source = typeof(StartStorageBlobCopyCmdlet).GetProperty(
            nameof(StartStorageBlobCopyCmdlet.AbsoluteUri))!;

        Assert.True(cmdlet.SupportsShouldProcess);
        Assert.Contains(
            source.GetCustomAttributes<ParameterAttribute>(),
            parameter => parameter.Mandatory && parameter.ValueFromPipelineByPropertyName);
        Assert.Contains("SourceUri", source.GetCustomAttribute<AliasAttribute>()!.AliasNames);
    }

    [Fact]
    public void NewStorageBlobSasTokenRequiresPermissionAndExpiry()
    {
        PropertyInfo permission = typeof(NewStorageBlobSasTokenCmdlet).GetProperty(
            nameof(NewStorageBlobSasTokenCmdlet.Permission))!;
        PropertyInfo expiry = typeof(NewStorageBlobSasTokenCmdlet).GetProperty(
            nameof(NewStorageBlobSasTokenCmdlet.ExpiryTime))!;

        Assert.True(permission.GetCustomAttribute<ParameterAttribute>()!.Mandatory);
        Assert.True(expiry.GetCustomAttribute<ParameterAttribute>()!.Mandatory);
        Assert.Equal(
            typeof(SwitchParameter),
            typeof(NewStorageBlobSasTokenCmdlet).GetProperty(
                nameof(NewStorageBlobSasTokenCmdlet.FullUri))!.PropertyType);
    }
}
