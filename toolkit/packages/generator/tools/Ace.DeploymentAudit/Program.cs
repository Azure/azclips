using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ace.DeploymentAudit;
using Ace.Generator.Model.Ir;
using Azure.Bicep.Types.Az;

const string pinnedCatalogue = "Azure.Bicep.Types.Az@0.2.972";
Console.InputEncoding = new UTF8Encoding(false, true);
Console.OutputEncoding = new UTF8Encoding(false);
var jsonOptions = new JsonSerializerOptions(IrJson.Options) { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
try
{
    string input = await Console.In.ReadToEndAsync();
    string? catalogue = null;
    var selections = new List<string>();
    for (int index = 0; index < args.Length; index += 2)
    {
        if (index + 1 == args.Length) throw new AuditInputException("argument-value-missing", $"Missing value for '{args[index]}'.");
        switch (args[index])
        {
            case "--catalogue" when catalogue is null: catalogue = args[index + 1]; break;
            case "--select": selections.Add(args[index + 1]); break;
            default: throw new AuditInputException("argument-unsupported", $"Unknown or repeated argument '{args[index]}'.");
        }
    }
    if (catalogue != pinnedCatalogue) throw new AuditInputException("catalogue-pin-required", $"Explicitly select --catalogue {pinnedCatalogue}.");
    Assembly assembly = typeof(AzTypeLoader).Assembly;
    string? actualVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];
    if (actualVersion != "0.2.972") throw new AuditInputException("catalogue-pin-mismatch", $"The loaded catalogue version is '{actualVersion}', not the selected pin.");
    TransformedIR ir = IrJson.Deserialize(input);
    AuditReport report = CompatibilityAudit.Run(ir, new AzTypeLoader(), selections, pinnedCatalogue,
        Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(assembly.Location))),
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input))));
    Console.WriteLine(JsonSerializer.Serialize(report, jsonOptions));
    return report.ExitCode;
}
catch (AuditInputException error)
{
    return Fail(error.Code, error.Message);
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
{
    return Fail("audit-input-unreadable", error.Message);
}

int Fail(string code, string message)
{
    Console.Error.WriteLine($"deployment-audit: {message}");
    Console.WriteLine(JsonSerializer.Serialize(new {
        schemaVersion = "1.0.0", status = "unsupported", exitCode = 1,
        diagnostics = new[] { new AuditDiagnostic(code, "$", message) },
    }, jsonOptions));
    return 1;
}
