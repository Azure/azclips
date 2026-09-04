// Ace.Generator.Host — out-of-process entry point for the C# generator.
//
// Contract with the compiler's built-in `csharp-generate` tool:
//   stdin  : one JSON GenerateToolRequest  { ir, options: { rootNamespace?, config? } }
//   stdout : NDJSON frames — one {"kind":"file","path","contents"} per generated
//            file (streamed as produced), then a terminal
//            {"kind":"result","success","diagnostics":[{severity,code,message}]}.
//
// All framing/translation lives in HostRunner (unit-tested); this entry point
// just wires it to the process std streams.

using Ace.Generator.Host;

return await HostRunner.RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput());
