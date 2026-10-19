using Core.Agent.Hooks;
using GitHub.Copilot;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Unit tests for <see cref="FailedCommandFiles"/>: a read of a file that the failed
/// command names needs no approval, and every other request still asks the user.
/// </summary>
public sealed class FailedCommandFilesTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("azclips-command-files-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("deployment group create -g rg --template-file fixtures/web-app.bicep")]
    [InlineData("deployment group create -g rg --template-file=fixtures/web-app.bicep")]
    [InlineData("deployment group create -g rg --parameters @fixtures/web-app.bicep")]
    [InlineData("deployment group create -g rg --template-file \"fixtures/web-app.bicep\"")]
    public void Allows_a_read_of_a_file_that_the_failed_command_names(string command)
    {
        var path = CreateFile("fixtures/web-app.bicep");
        var files = FailedCommandFiles.From(command, _directory);

        Assert.True(files.Allows(Read(path)));
        // The model can also give the path relative to the working directory.
        Assert.True(files.Allows(Read("fixtures/web-app.bicep")));
    }

    [Fact]
    public void Still_asks_for_other_files_directories_and_requests()
    {
        var named = CreateFile("fixtures/web-app.bicep");
        var other = CreateFile("fixtures/other.bicep");
        var files = FailedCommandFiles.From(
            "deployment group create --template-file fixtures/web-app.bicep --name fixtures", _directory);

        Assert.False(files.Allows(Read(other)));
        // "fixtures" names a directory, not a file.
        Assert.False(files.Allows(Read(Path.Combine(_directory, "fixtures"))));
        Assert.False(files.Allows(new PermissionRequestRead
        {
            Path = named, Intention = "Read the file", RequestSandboxBypass = true,
        }));
        Assert.False(files.Allows(new PermissionRequestWrite
        {
            FileName = named, Intention = "Edit the file", Diff = string.Empty, CanOfferSessionApproval = false,
        }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("deployment group create --template-file missing.bicep")]
    public void Allows_nothing_when_the_command_names_no_existing_file(string? command)
    {
        CreateFile("present.bicep");
        var files = FailedCommandFiles.From(command, _directory);

        Assert.False(files.Allows(Read("missing.bicep")));
        Assert.False(files.Allows(Read("present.bicep")));
    }

    private string CreateFile(string relativePath)
    {
        var path = Path.Combine(_directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "param name string");
        return path;
    }

    private static PermissionRequestRead Read(string path) => new() { Path = path, Intention = "Read the file" };
}
