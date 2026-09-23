// IR model code generator (entry point).
//
// Reads the IR JSON Schema (derived from the Zod source of truth in
// @azure-client-engine/ir) and emits C# POCOs so the generator never
// hand-duplicates the IR definition. The JSON-Schema -> C# mapping lives in
// IrModelGenerator and uses Roslyn to emit code — no third-party codegen library.
//
// Usage:
//   dotnet run --project tools/AceIrModelGen -- \
//       --schema  <path to ir.schema.json> \
//       --out     <path to write the generated .cs> \
//       --namespace <C# namespace> \
//       --attributes-out <path to write generated CLR attributes> \
//       --attributes-namespace <C# namespace>
//
// Defaults are relative to the generator package root (this tool's working
// directory when run from there), so a no-argument run works once the schema
// has been emitted.

using Ace.Ir.CodeGen;

string schemaPath = GetArg("--schema") ?? "../ir/dist/ir.schema.json";
string outPath = GetArg("--out") ?? "src/Model/Ir/Ir.Generated.cs";
string ns = GetArg("--namespace") ?? "Ace.Generator.Model.Ir";
string attributesOutPath = GetArg("--attributes-out") ?? "../../../src/Core/Core.Abstractions/PowerShellContracts.Generated.cs";
string attributesNamespace = GetArg("--attributes-namespace") ?? "Core.Abstractions";

if (!File.Exists(schemaPath))
{
    Console.Error.WriteLine(
        $"IR schema not found at '{Path.GetFullPath(schemaPath)}'.\n" +
        "Emit it first, e.g. `npm run schema -w @azure-client-engine/ir` (writes to packages/ir/dist).");
    return 1;
}

string schemaJson = await File.ReadAllTextAsync(schemaPath);
string code = new IrModelGenerator(schemaJson, ns).Generate();
string attributeCode = new CSharpAttributeGenerator(schemaJson, attributesNamespace).Generate();

string fullOut = Path.GetFullPath(outPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullOut)!);
await File.WriteAllTextAsync(fullOut, code);

string fullAttributesOut = Path.GetFullPath(attributesOutPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullAttributesOut)!);
await File.WriteAllTextAsync(fullAttributesOut, attributeCode);

Console.WriteLine($"Wrote {fullOut} ({code.Split('\n').Length} lines) from {Path.GetFullPath(schemaPath)}.");
Console.WriteLine($"Wrote {fullAttributesOut} ({attributeCode.Split('\n').Length} lines) from CLR attribute projections.");
return 0;

string? GetArg(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

