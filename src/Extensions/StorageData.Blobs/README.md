# Mock StorageData Blobs extension

This mock-only commit requires the shared-group hint and help fixes from
[Azure/azclips#1404](https://github.com/Azure/azclips/pull/1404). Cherry-pick it
onto a branch containing those fixes; the commit itself does not change the
loader, hint generator, or help dispatcher.

This test-branch extension demonstrates two independently built extensions sharing
the `az storage` group:

| Command | Extension assembly |
| --- | --- |
| `az storage account ...` | `Microsoft.Azure.Azclips.Extensions.Storage.dll` |
| `az storage blob` | `Microsoft.Azure.Azclips.Extensions.StorageData.Blobs.dll` |

From the repository root in PowerShell:

```powershell
dotnet build azclips.slnx
& .\artifacts\Debug\Core.Cli\az.exe storage --help
& .\artifacts\Debug\Core.Cli\az.exe storage account list --help
& .\artifacts\Debug\Core.Cli\az.exe storage blob --output json
```

The blob command takes no arguments and returns a mock result identifying its
actual executing assembly. It does not authenticate or make Azure requests.
The existing Storage commands are unchanged; executing them still requires their
normal Azure configuration.

The project uses the normal extension build, sidecar generation, NuGet packaging,
discovery, and dispatch paths. No loader changes are needed. Build-time install
hints omit an ambiguous bare `storage` entry, retain the precise command-to-package
entries, and still reject duplicate command leaves. Group and command help combine
the commands contributed by both extensions.
