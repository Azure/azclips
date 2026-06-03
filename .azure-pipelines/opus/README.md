# Opus signing suite

A simplified ESRP code-signing pipeline for cirrus, designed to run **inside the ADO `release` project** alongside `cirrus-build`. It mirrors the structural shape of `.azure-pipelines/cirrus-signing.yml` + `.azure-pipelines/templates/dist/stages/sign-with-esrp.yml`, while adding a few functional improvements (script signing, third-party DLL signing, MSI signing, per-pass `SigntoolVerify`, RFC3161 timestamp, test-DLL exclusion).

There is **no cross-project orchestration**, no REST triggering, and no storage push — that is owned by `cirrus-publish.yml` in the existing setup.

## Layout

```
.azure-pipelines/opus/
├── cirrus-sign.yml                  # Entry pipeline (preflight + template call)
├── util/
│   └── code-sign-stages.yml         # Multi-stage signing template
├── scripts/
│   ├── Stage-SigningInputs.ps1      # Stage downloaded build outputs for signing
│   ├── Repack-WindowsZips.ps1       # Re-zip the (now-signed) Windows payloads
│   ├── Collect-SignedArtifacts.ps1  # Copy signed nupkg / msi to release root
│   ├── Verify-Signatures.ps1        # Authenticode / dotnet nuget verify
│   └── New-ReleaseManifest.ps1      # SHA-256 ReleaseManifest.txt
└── README.md
```

## Flow

```
cirrus-build  ──► (artifacts: *win-*.zip, *.nupkg, *linux*.tar.gz, *macos*.tar.gz, *.msi)
                          │
                          ▼
                   cirrus-sign.yml  (Preflight: System.TeamProject == 'release')
                          │
                          ▼
              util/code-sign-stages.yml
              │
              ├─ SignNugetArtifacts        ─► artifact: signed-nuget
              ├─ SignWindowsArtifacts      ─► artifact: signed-windows
              ├─ CollectLinuxArtifacts     ─► artifact: collected-linux
              ├─ CollectMacOSArtifacts     ─► artifact: collected-macos
              │      (above 4 stages run in parallel, all dependsOn: Preflight)
              │
              ├─ ValidateNugetArtifacts    (dotnet nuget verify --all)
              ├─ ValidateWindowsArtifacts  (Get-AuthenticodeSignature)
              ├─ ValidateLinuxArtifacts    (presence check)
              ├─ ValidateMacOSArtifacts    (presence check)
              │
              └─ AssembleSignedArtifacts   ─► artifact: <signed_artifact_name>
                                              (default: Distribution_Signed)
                                              + signing-manifest (ReleaseManifest.txt)
```

## Required ADO context

- `System.TeamProject` must equal `release` (Preflight enforces this).
- Variable group **`AME ESRP Variable Group`** is conditionally loaded for the `release` project; it must provide:
  - `ESRPAppClientId`
  - `ESRPAppTenantId`
  - `ESRPKVName`
  - `ESRPAuthCertName`
  - `ESRPSignCertName`
- Service connection (default `ame_esrp_connection`) must be authorized for the pipeline.

## Parameters (cirrus-sign.yml)

| Name                    | Default                | Purpose                                                                |
| ----------------------- | ---------------------- | ---------------------------------------------------------------------- |
| `source_pipeline_name`  | `cirrus-build`         | Definition name of the build pipeline whose artifacts are signed.      |
| `source_build_id`       | _(required at run)_    | Run id of the `cirrus-build` execution to download artifacts from.     |
| `signed_artifact_name`  | `Distribution_Signed`  | Name of the final aggregated artifact published by `AssembleSignedArtifacts`. |
| `ESRPServiceConnection` | `ame_esrp_connection`  | ADO service connection used by `EsrpCodeSigning@5`.                    |

## ESRP key codes

| KeyCode      | Used for                                                                         |
| ------------ | -------------------------------------------------------------------------------- |
| `CP-230012`  | 1P Authenticode — Cirrus binaries, scripts (`*.ps1`/`*.psm1`/`*.psd1`/`*.ps1xml`/`*.js`), and MSI installer. |
| `CP-231522`  | 3P Authenticode (Append `/as`) — third-party DLLs (Newtonsoft.Json, Azure SDK, etc.). |
| `CP-401405`  | NuGet — `NuGetSign` + `NuGetVerify`.                                             |

## What we sign

| Asset class       | Pattern                                                                | Pass                                |
| ----------------- | ---------------------------------------------------------------------- | ----------------------------------- |
| Cirrus binaries   | `**/azv2.exe`, `**/Microsoft.Azure.Cirrus*.dll`/`.exe`, `**/Cirrus*.dll`, `**/Az.*.dll` | 1P Authenticode + `SigntoolVerify`  |
| Test DLLs         | `!**/*.Test.dll`, `!**/*Test*.dll`, `!**/*.Tests.dll`                  | _excluded_                          |
| Scripts           | `**/*.ps1`, `**/*.psm1`, `**/*.psd1`, `**/*.ps1xml`, `**/*.js`         | 1P Authenticode + `SigntoolVerify`  |
| Third-party DLLs  | `**/*.dll` minus the above                                             | 3P Authenticode (`Append /as`) + `SigntoolVerify` |
| MSI               | `*.msi`                                                                | 1P Authenticode + `SigntoolVerify`  |
| NuPkg             | `Microsoft.Azure.Cirrus*.nupkg`                                        | `NuGetSign` + `NuGetVerify`         |
| Linux / macOS     | `*linux*.tar.gz/zip`, `*mac*/*macos*/*osx*/*darwin*.tar.gz/zip`        | _passed through unsigned_           |

## ESRP knobs (per signing pass)

- `SessionTimeout: '60'`
- `MaxConcurrency: '25'`
- `MaxRetryAttempts: '5'`
- RFC3161 timestamp: `/tr "http://rfc3161.gtm.corp.microsoft.com/TSS/HttpTspServer" /td sha256` (passed via the `TimeStamp` parameter to `SigntoolSign`).

## How to run

1. Wait for a green `cirrus-build` run in the `release` project.
2. Queue `cirrus-sign` (UI → Run pipeline).
3. Provide `source_build_id` = the build id of the `cirrus-build` run.
4. (Optional) override `source_pipeline_name`, `signed_artifact_name`, or `ESRPServiceConnection`.

The final signed drop is published as the artifact named by `signed_artifact_name` (default `Distribution_Signed`) on the `cirrus-sign` run, and includes `ReleaseManifest.txt` with SHA-256 hashes for every file. `cirrus-publish.yml` (or the equivalent release pipeline) picks up that artifact for storage upload.

## Differences vs the legacy `cirrus-signing.yml`

| Aspect                          | Legacy                                  | Opus                                                              |
| ------------------------------- | --------------------------------------- | ----------------------------------------------------------------- |
| Stages                          | Sign{Nuget,Windows} + Collect{Linux,Mac} + Validate{...} + Assemble | _Same shape_                                                      |
| 1P Authenticode KeyCode         | `CP-230012`                             | `CP-230012`                                                       |
| Per-pass `SigntoolVerify`       | ❌                                       | ✅ (each ESRP pass appends a verify operation)                    |
| RFC3161 timestamp               | ❌                                       | ✅                                                                 |
| Script signing (`ps1`/`psm1`/…) | ❌                                       | ✅                                                                 |
| 3P DLL signing (`Append /as`)   | ❌                                       | ✅ (`CP-231522`)                                                   |
| MSI signing                     | ❌                                       | ✅ (when `HasMsi=true`)                                            |
| Test-DLL exclusion              | implicit (only `Cirrus*` patterns)      | explicit (`!**/*Test*.dll`)                                       |
| NuGet verifier                  | `dotnet nuget verify --all`             | `dotnet nuget verify --all`                                       |
| Manifest                        | `signing-manifest.json` (Python heredoc) | `ReleaseManifest.txt` (PowerShell `Get-FileHash`)                 |
