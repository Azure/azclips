// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.IO.Pipes;
using System.Text.Json;

namespace Cli.Tests.Fixtures.FakeAgent;

/// <summary>
/// Stands in for azclips-agent: reads the recovery request from the input pipe, records its argv,
/// environment and request to the file named by AZCLIPS_TEST_FAKE_AGENT_OUT, and prints a fixed plan.
/// </summary>
internal static class FakeAgent
{
    public static int Main(string[] args)
    {
        var index = Array.IndexOf(args, "--recovery-input");
        if (index < 0 || index + 1 >= args.Length)
            return 64;

        string request;
        using (var input = new AnonymousPipeClientStream(PipeDirection.In, args[index + 1]))
        using (var reader = new StreamReader(input))
            request = reader.ReadToEnd();

        var environment = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);
        var output = Environment.GetEnvironmentVariable("AZCLIPS_TEST_FAKE_AGENT_OUT");
        if (!string.IsNullOrEmpty(output))
            File.WriteAllText(output, JsonSerializer.Serialize(new { args, environment, request }));

        Console.Error.WriteLine("AI PLAN");
        return 0;
    }
}
