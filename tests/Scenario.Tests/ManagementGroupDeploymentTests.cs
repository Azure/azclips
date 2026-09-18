// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Scenario.Tests;

public class ManagementGroupDeploymentTests
{
    [Fact]
    public async Task LocalTemplateIsFlattenedIntoRequestBody()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mg-deployment-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, """{"contentVersion":"1.0.0.0","resources":[]}""");

        try
        {
            TestProcessor processor = new()
            {
                ManagementGroupId = "test-management-group",
                Location = "eastus",
                TemplateFile = path,
            };

            await processor.PrepareAsync();
            Deployment body = processor.RequestBody();

            Assert.Equal(Path.GetFileNameWithoutExtension(path), processor.Name);
            Assert.Equal("eastus", body.Location);
            Assert.NotNull(body.Properties.Template);
            Assert.Equal(DeploymentMode.Incremental, body.Properties.Mode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class TestProcessor : NewManagementGroupDeploymentProcessor_Orchestrator
    {
        public Task PrepareAsync() => BeforeProcessAsync();

        public Deployment RequestBody() => CreateRequestBody()!;
    }
}
