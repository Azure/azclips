---
summary: Transfer and inspect Azure Blob and Files data using Entra profiles.
status: preview
schema: 1.0.0
online version:
---

Description: Manage the reviewed Azure Blob and Azure Files data-plane workflows, plus explicit ARM account-key listing. Use a public-Azure Entra profile. Storage requests do not require a subscription; account-key listing does. Account keys returned by the explicit command are never used as an authentication fallback. Destination SAS, account-key, connection-string, anonymous, emulator and sovereign-cloud authentication are not supported.

### Commands

Blob: `upload`, `download`, `download-batch`, `list`, `show`, `exists`, `delete`, `set-tier`, and `copy start`.

Files: `file upload`, `file download`, `file list`, `directory create`, and `directory list`.

ARM: `account keys list`. This intentionally returns secrets; avoid logging its output.

### Upload a local file

```bash
az storage blob upload --account-name myaccount --container-name content --file report.csv --auth-mode login
az storage blob upload --account-name myaccount --container-name content --name report.csv --file report.csv --overwrite true --metadata source=example --tags reviewed
```

The blob name defaults to the local filename. Type defaults to Block, except a `.vhd` source defaults to Page. Page sources and chunks must be 512-byte aligned. Block/Page uploads protect against overwriting unless requested. Append uploads append without truncation unless overwrite is selected; an empty CLI Append source is a no-op.

Explicit content settings take precedence. Common extensions have stable MIME inference with an octet-stream fallback; inference does not consult OS-specific MIME registries. Use `--content-type` for an exact MIME contract. Content-MD5 is a Base64-encoded 16-byte value.

### Download to disk or write exact binary stdout

```bash
az storage blob download --account-name myaccount --container-name content --name report.csv --file downloaded.csv
az storage blob download --account-name myaccount --container-name content --name report.csv > downloaded.csv
```

Single CLI Blob downloads overwrite local files by default; pass `--overwrite false` to protect them. Omitting `--file` writes only raw bytes, without JSON, text decoding or a trailing newline. Diagnostics remain on stderr. Binary output cannot be queried with `--query`; download to a file to query the resulting properties. A failed or cancelled transfer may leave partial local bytes and never reports completion.

### Plan and download a batch

```bash
az storage blob download-batch --account-name myaccount --source content --destination existing-folder --pattern "*.csv" --dryrun
az storage blob download-batch --account-name myaccount --source content --destination existing-folder --pattern "*.csv"
```

Batch overwrite defaults to false. Patterns use Python fnmatch semantics, including matching separators and host-platform case/separator normalization, not PowerShell wildcard semantics. All selected paths and collisions are checked before local writes. Completed names, skipped preconditions and failures remain visible; a batch can stop after partial progress.

### Start a normal server-side copy

```bash
az storage blob copy start --account-name destaccount --destination-container content --destination-blob copied.csv --source-account-name sourceaccount --source-container content --source-blob original.csv
```

Copy overwrites by default; use `--destination-if-none-match "*"` for create-only protection. An unsigned Blob source uses short-lived source-profile delegation. A source SAS can instead be supplied in `--source-uri` or `--source-sas`. Do not put SAS values in logs. CLI uses the selected profile for independently resolved accounts. The output is an acknowledgment with exactly `copyId`, `copyStatus`, `etag` and UTC `lastModified`, not completion polling. Source leases, type conversion and requires-sync are not registered.

For a Files source, use a supplied service/account SAS, a supplied `--source-account-key`, or opt in to ARM key lookup:

```bash
az storage blob copy start --account-name destaccount --destination-container content --destination-blob copied.csv --source-account-name sourceaccount --source-share reports --source-path original.csv --source-sas "$SOURCE_FILE_SAS"
az storage blob copy start --account-name destaccount --destination-container content --destination-blob copied.csv --source-account-name sourceaccount --source-share reports --source-path original.csv --source-account-key "$SOURCE_STORAGE_KEY"
az storage blob copy start --account-name destaccount --destination-container content --destination-blob copied.csv --source-account-name sourceaccount --source-share reports --source-path original.csv --use-source-account-key-lookup --source-key-resource-group source-rg
```

ARM lookup requires permission to list source account keys. `--source-key-subscription-id` defaults to the selected profile subscription; `--source-key-name` defaults to `key1` and can select `key2` during rotation. Supplied keys and ARM lookup are alternative modes. The key only signs a read-only file SAS valid for one hour, with a five-minute start-time allowance; it is not persisted or returned. A supplied SAS takes precedence over all key options, and a failed SAS never falls back to key lookup.

The source account must allow Shared Key authorization. Destination requests remain Entra-authenticated and the destination can keep Shared Key disabled. Files user-delegation SAS is not supported for normal asynchronous Blob copy: the service does not forward its required backup-intent header.

### Azure Files backup intent

```bash
az storage file list --account-name myaccount --share-name content --enable-file-backup-request-intent
```

Files OAuth content/listing operations require explicit backup intent and suitable data-plane roles. Backup intent alone cannot authorize normal Files-to-Blob copy; use one of the source-authorization modes above. A presigned source does not require source-profile authentication.

### Transfer limits and PowerShell

Transfers use two workers by default, allow one through four, and bound chunks to 8 MiB for Blob or 4 MiB for Page/Files. Use `--max-connections`, `--chunk-size`, `--validate-content` and `--no-progress` where shown by command help. These bounded defaults intentionally differ from the legacy PowerShell nominal concurrency default of ten.

The Storage PowerShell module returns native Azclips resources, not legacy SDK CLR objects. Content commands support their documented native pipelines and invocation-thread confirmation behavior. `Force` does not bypass `WhatIf` or `Confirm`. Files requires a context constructed with `EnableFileBackupRequestIntent`. See each cmdlet's help for parameter sets and PassThru behavior.

### Acceptance scripts

The repository's CLI and PowerShell Storage smoke scripts require explicit live-test opt-in. They use a dedicated temporary resource group, separate small Standard LRS destination/Files-source accounts and small payloads. Only the source account enables Shared Key. They check all three Files-copy source modes and then delete and verify removal of their resources, including on failure. The caller-supplied SAS fixture is generated locally by Azure CLI; migrated operations run through the built Azclips CLI or PowerShell module. CLI and PowerShell use separate sign-in applications and both must be authenticated. The scripts never accept an existing resource group for deletion and do not run the separate 5 GiB SHA-256/working-set acceptance test.

The separate [large-transfer harness](../../../../../tests/Scripts/StoragePerformance.ps1) requires explicit `-AllowLive` and prior provisioning CLI, Azclips CLI and PowerShell sign-ins. Run it from the repository root after building the solution:

```powershell
.\tests\Scripts\StoragePerformance.ps1 `
    -TenantId '<tenant-id>' -SubscriptionId '<subscription-id>' `
    -CliProfile '<cli-profile>' -PowerShellProfile '<powershell-profile>' `
    -ResultPath '.\storage-performance-results.json' -Location australiaeast -AllowLive
```

Use `-ProvisioningCli` when Azure CLI is not the default `az` executable, and choose a new result filename for each run. The default test performs eight actual 5 GiB transfers: upload and download for Blob and Files on each host (20 GiB uploaded and 20 GiB downloaded). It creates one owned temporary Australian Standard LRS account with Shared Key disabled and cleans up its Azure resources and local payloads even after failure. Azure usage and transfer charges may apply; authorize this separately from small smoke tests.

Each operation uses four workers and at most 8 MiB Blob / 4 MiB Files chunks in a fresh process. After a 1 MiB warmup round trip, the harness takes the median of 20 idle working-set samples, samples the transfer every 25 ms and conservatively includes the OS lifetime peak. The CLI runs its actual public entrypoint in a warmed host with the original CLI runtime configuration. No forced GC, heap overrides or working-set trimming are used. Acceptance requires exact 5 GiB lengths, matching download SHA-256 values and no more than 256 MiB additional working set per operation. Throughput is recorded without a numeric pass threshold. Results and cleanup status are persisted as JSON; a failed transfer or memory limit makes the run fail.
