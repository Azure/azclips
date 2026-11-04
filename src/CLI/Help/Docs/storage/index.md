---
summary: Manage Azure Storage accounts and transfer Azure Blob and Files data.
status: preview
schema: 1.0.0
online version:
---

Description: Manage Azure Storage accounts and Azure Blob and Azure Files data-plane workflows. Use a public-Azure Entra profile. Storage data-plane requests do not require a subscription; explicit copy-source ARM key lookup does. Destination SAS, account-key, connection-string, anonymous, emulator and sovereign-cloud authentication are not supported.

### Commands

Blob: `upload`, `download-batch`, `list`, `show`, `exists`, `delete`, `set-tier`, and `copy start`.

Files: `file upload`, `file download`, `file list`, and `directory list`.

ARM: `account create`, `account show`, `account list`, and `account delete` (existing management commands).

Data-plane commands follow the explicit command list in [issue #660](https://github.com/Azure/CLIPS/issues/660). Standalone CLI Blob download, CLI directory creation, public account-key listing, and PowerShell Files content transfers are outside this scope. SAS generation commands remain deferred as optional. Azure CLI has no `directory upload` or `directory download` commands.

### Discover storage commands

```bash
az storage --help
```

### Create a storage account

```bash
az storage account create --resource-group myResourceGroup --name mystorageaccount --location eastus --sku Standard_LRS --kind StorageV2
```

### Show, list and delete storage accounts

```bash
az storage account show --resource-group myResourceGroup --name mystorageaccount
az storage account list --resource-group myResourceGroup
az storage account delete --resource-group myResourceGroup --name mystorageaccount
```

### Upload a local file

```bash
az storage blob upload --account-name myaccount --container-name content --file report.csv --auth-mode login
az storage blob upload --account-name myaccount --container-name content --name report.csv --file report.csv --overwrite true --metadata source=example --tags reviewed
```

The blob name defaults to the local filename. Type defaults to Block, except a `.vhd` source defaults to Page. Page sources and chunks must be 512-byte aligned. Block/Page uploads protect against overwriting unless requested. Append uploads append without truncation unless overwrite is selected; an empty CLI Append source is a no-op.

Explicit content settings take precedence. Common extensions have stable MIME inference with an octet-stream fallback; inference does not consult OS-specific MIME registries. Use `--content-type` for an exact MIME contract. Content-MD5 is a Base64-encoded 16-byte value.

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

ARM lookup requires permission to list source account keys. `--source-key-subscription-id` defaults to the selected profile subscription; `--source-key-name` defaults to `key1` and can select `key2` during rotation. Supplied keys and ARM lookup are alternative modes. The key only signs a read-only file SAS valid for one day, with a five-minute start-time allowance; it is not persisted or returned. A supplied SAS takes precedence over all key options, and a failed SAS never falls back to key lookup.

The source account must allow Shared Key authorization. Destination requests remain Entra-authenticated and the destination can keep Shared Key disabled. Files user-delegation SAS is not supported for normal asynchronous Blob copy: the service does not forward its required backup-intent header.

### Azure Files backup intent

```bash
az storage file list --account-name myaccount --share-name content --enable-file-backup-request-intent
```

Files OAuth content/listing operations require explicit backup intent and suitable data-plane roles. Backup intent alone cannot authorize normal Files-to-Blob copy; use one of the source-authorization modes above. A presigned source does not require source-profile authentication.

### Transfer limits and PowerShell

Transfers use two workers by default, allow one through four, and bound chunks to 8 MiB for Blob or 4 MiB for Page/Files. Use `--max-connections`, `--chunk-size`, `--validate-content` and `--no-progress` where shown by command help. These bounded defaults intentionally differ from the legacy PowerShell nominal concurrency default of ten.

The Storage PowerShell module returns native Azclips resources, not legacy SDK CLR objects. Content commands support their documented native pipelines and invocation-thread confirmation behavior. `Force` does not bypass `WhatIf` or `Confirm`. Files requires a context constructed with `EnableFileBackupRequestIntent`. See each cmdlet's help for parameter sets and PassThru behavior.

The added PowerShell cmdlets are `New-AzStorageContext`, `Get-AzStorageBlob`, `Remove-AzStorageBlob`, `Set-AzStorageBlobContent`, `Get-AzStorageBlobContent`, `Start-AzStorageBlobCopy`, `Get-AzStorageFile`, and `New-AzStorageDirectory`. Files content transfers are available through the CLI only.
