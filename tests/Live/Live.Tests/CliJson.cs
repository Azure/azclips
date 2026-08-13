using System.Text.Json;

namespace Live.Tests;

/// <summary>
/// Deserializes the JSON a CLI command wrote to stdout.
///
/// <para>
/// The <c>…Output</c> records these methods produce are this suite's statement of
/// the CLI's output contract. They are deliberately declared here rather than
/// shared with the product's own models: these tests drive the published binary as
/// a black box, so a rename on the product side must show up as a failure here, not
/// be silently carried along by a project reference.
/// </para>
/// </summary>
internal static class CliJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Deserializes the object a command wrote, e.g. from <c>profile show</c>.</summary>
    public static T Parse<T>(AzResult result, string command)
        => Deserialize<T>(result, command, JsonSerializer.Deserialize<T>);

    /// <summary>
    /// Reports a broken contract as an assertion failure naming the command and
    /// showing its output, so it reads as "this command's JSON changed" rather than
    /// as an unhandled JsonException from somewhere inside a test helper.
    /// </summary>
    private static T Deserialize<T>(
        AzResult result,
        string command,
        Func<string, JsonSerializerOptions, T?> deserialize)
    {
        T? value;
        try
        {
            value = deserialize(result.StandardOutput, Options);
        }
        catch (JsonException exception)
        {
            Assert.Fail(
                $"'{command}' returned unexpected JSON ({exception.Message})."
                    + $"{Environment.NewLine}{result.Describe()}");
            throw;
        }

        Assert.True(
            value is not null,
            $"'{command}' returned no JSON.{Environment.NewLine}{result.Describe()}");

        return value!;
    }
}
