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
        // A relative read path is not approved: the read tool would take it from the agent's own directory.
        Assert.False(files.Allows(Read("fixtures/web-app.bicep")));
    }

    [Fact]
    public void A_quoted_path_with_a_space_is_one_argument_and_its_fragments_name_nothing()
    {
        var named = CreateFile("project/web app.bicep");
        var prefix = CreateFile("project/web");
        var suffix = CreateFile("app.bicep");
        var files = FailedCommandFiles.From(
            "deployment group create --template-file \"project/web app.bicep\"", _directory);

        Assert.True(files.Allows(Read(named)));
        Assert.False(files.Allows(Read(prefix)));
        Assert.False(files.Allows(Read(suffix)));
    }

    [Fact]
    public void An_argument_with_an_unterminated_quote_names_nothing()
    {
        var named = CreateFile("project/web app.bicep");
        var prefix = CreateFile("project/web");
        var files = FailedCommandFiles.From(
            "deployment group create --template-file \"project/web app.bicep", _directory);

        Assert.False(files.Allows(Read(named)));
        Assert.False(files.Allows(Read(prefix)));
    }

    [Fact]
    public void Only_an_option_carries_a_value_after_an_equals_sign()
    {
        var suffix = CreateFile("app.bicep");
        var files = FailedCommandFiles.From("deployment group create --parameters name=app.bicep", _directory);

        Assert.False(files.Allows(Read(suffix)));
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

        Assert.False(files.Allows(Read(Path.Combine(_directory, "missing.bicep"))));
        Assert.False(files.Allows(Read(Path.Combine(_directory, "present.bicep"))));
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

// These tests set AZCLIPS_CALLER_CWD, which is process-wide, so they must not overlap any other test.
[CollectionDefinition("CallerDirectory", DisableParallelization = true)]
public class CallerDirectoryCollection { }

[Collection("CallerDirectory")]
public sealed class FailedCommandFilesCallerTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("azclips-caller-files-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Relative_paths_resolve_from_the_directory_that_the_CLI_forwarded()
    {
        const string command = "deployment group create --template-file fixtures/web-app.bicep";
        var path = Path.Combine(_directory, "fixtures", "web-app.bicep");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "param name string");
        var read = new PermissionRequestRead { Path = path, Intention = "Read the template" };
        var previous = Environment.GetEnvironmentVariable(FailedCommandFiles.CallerDirectoryVariable);
        try
        {
            // The CLI starts the agent in another directory and forwards the caller's directory.
            Assert.NotEqual(Path.GetFullPath(_directory), Path.GetFullPath(Directory.GetCurrentDirectory()));
            Environment.SetEnvironmentVariable(FailedCommandFiles.CallerDirectoryVariable, _directory);
            Assert.True(FailedCommandFiles.ForCaller(command).Allows(read));

            // Without the variable, the path resolves from the agent's own directory, where no such file is.
            Environment.SetEnvironmentVariable(FailedCommandFiles.CallerDirectoryVariable, null);
            Assert.False(FailedCommandFiles.ForCaller(command).Allows(read));
        }
        finally
        {
            Environment.SetEnvironmentVariable(FailedCommandFiles.CallerDirectoryVariable, previous);
        }
    }
}
