# Scenario 5: Store and retrieve application data

**User goal:** "I want my application to store a file in Azure, retrieve the same data and remove it when it is no longer needed."

**Successful outcome:** Blob and file-share round trips preserve the test file's contents, and all run-owned data and storage resources are removed.

## Prerequisites

- The AzCLIPS CLI and/or the preview Az PowerShell modules, installed and signed in as described in [Scenario 1](https://github.com/Azure/azclips/blob/docs/scenario-2-feedback/docs/scenario-1-get-started-and-profiles.md). The modules require PowerShell 7.6+.
- Permission to upload and download data in the test storage account.

Use only synthetic test data.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash**, **AzCLIPS CLI in PowerShell** or **AzCLIPS PowerShell**. Each path follows the same steps.

- **1. Create the storage environment:** Create a dedicated resource group, a storage account and a private blob container.
- **2. Upload data:** Create a local test file, upload it as a blob and list it.
- **3. Retrieve data:** Download the blob and confirm its contents match the original.
- **4. Share a file (optional):** Upload the same file to a file share, download it and confirm its contents.
- **5. Clean up:** Delete the resource group, which removes the uploaded data and storage resources, and remove the local files.

## AzCLIPS CLI in Bash

```bash
set -euo pipefail
sub="<test-subscription-id>"
location="<region>"
run=$(date +%Y%m%d%H%M%S)
rg="azclips-data-$run"
account="clips$(openssl rand -hex 9)"
container="data-$run"
share="files-$run"
source_file="/tmp/$rg-source.txt"
blob_download="/tmp/$rg-blob.txt"
file_download="/tmp/$rg-file.txt"
```

**1. Create the storage environment**

```bash
az group create --subscription-id "$sub" --name "$rg" --location "$location"
az storage account create --resource-group "$rg" --name "$account" --location "$location" --sku Standard_LRS --kind StorageV2
az storage account show --resource-group "$rg" --name "$account"
```

Create a private blob container named `$container` in `$account`: `<blob container create command>`

**2. Upload data**

```bash
printf 'AzCLIPS data round trip %s' "$run" > "$source_file"
```

- Upload `$source_file` as the blob `payload.txt`: `<blob upload command>`
- List the blobs in `$container`: `<blob list command>`

**3. Retrieve data**

- Download `payload.txt` to `$blob_download`: `<blob download command>`

```bash
cmp "$source_file" "$blob_download"
```

**4. Share a file (optional)**

- Create the file share `$share` in `$account`: `<file share create command>`
- Upload `$source_file` to the share: `<file upload command>`
- List the files in the share: `<file list command>`
- Download the file to `$file_download`: `<file download command>`

```bash
cmp "$source_file" "$file_download"
```

**5. Clean up**

```bash
az group delete --subscription-id "$sub" --name "$rg"
az group exists --subscription-id "$sub" --name "$rg"
rm -f "$source_file" "$blob_download" "$file_download"
```

## AzCLIPS CLI in PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$sub = '<test-subscription-id>'
$location = '<region>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-data-$run"
$account = "clips$([guid]::NewGuid().ToString('N').Substring(0, 18))"
$container = "data-$run"
$share = "files-$run"
$temp = [IO.Path]::GetTempPath()
$source = Join-Path $temp "$rg-source.txt"
$blobDownload = Join-Path $temp "$rg-blob.txt"
$fileDownload = Join-Path $temp "$rg-file.txt"
```

**1. Create the storage environment**

```powershell
az group create --subscription-id $sub --name $rg --location $location
az storage account create --resource-group $rg --name $account --location $location --sku Standard_LRS --kind StorageV2
az storage account show --resource-group $rg --name $account
```

Create a private blob container named `$container` in `$account`: `<blob container create command>`

**2. Upload data**

```powershell
Set-Content -LiteralPath $source -Value "AzCLIPS data round trip $run" -NoNewline
```

- Upload `$source` as the blob `payload.txt`: `<blob upload command>`
- List the blobs in `$container`: `<blob list command>`

**3. Retrieve data**

- Download `payload.txt` to `$blobDownload`: `<blob download command>`

```powershell
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $blobDownload).Hash) { throw 'Blob content mismatch.' }
```

**4. Share a file (optional)**

- Create the file share `$share` in `$account`: `<file share create command>`
- Upload `$source` to the share: `<file upload command>`
- List the files in the share: `<file list command>`
- Download the file to `$fileDownload`: `<file download command>`

```powershell
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $fileDownload).Hash) { throw 'File-share content mismatch.' }
```

**5. Clean up**

```powershell
az group delete --subscription-id $sub --name $rg
az group exists --subscription-id $sub --name $rg
Remove-Item -LiteralPath $source, $blobDownload, $fileDownload -ErrorAction SilentlyContinue
```

## AzCLIPS PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$sub = '<test-subscription-id>'
$location = '<region>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-data-$run"
$account = "clips$([guid]::NewGuid().ToString('N').Substring(0, 18))"
$container = "data-$run"
$share = "files-$run"
$temp = [IO.Path]::GetTempPath()
$source = Join-Path $temp "$rg-source.txt"
$blobDownload = Join-Path $temp "$rg-blob.txt"
$fileDownload = Join-Path $temp "$rg-file.txt"
```

**1. Create the storage environment**

```powershell
New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location
New-AzStorageAccount -ResourceGroupName $rg -Name $account -Location $location -SkuName Standard_LRS -Kind StorageV2
Get-AzStorageAccount -ResourceGroupName $rg -Name $account
```

Create a private blob container named `$container` in `$account`: `<blob container create command>`

**2. Upload data**

```powershell
Set-Content -LiteralPath $source -Value "AzCLIPS data round trip $run" -NoNewline
```

- Upload `$source` as the blob `payload.txt`: `<blob upload command>`
- List the blobs in `$container`: `<blob list command>`

**3. Retrieve data**

- Download `payload.txt` to `$blobDownload`: `<blob download command>`

```powershell
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $blobDownload).Hash) { throw 'Blob content mismatch.' }
```

**4. Share a file (optional)**

- Create the file share `$share` in `$account`: `<file share create command>`
- Upload `$source` to the share: `<file upload command>`
- List the files in the share: `<file list command>`
- Download the file to `$fileDownload`: `<file download command>`

```powershell
if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $fileDownload).Hash) { throw 'File-share content mismatch.' }
```

**5. Clean up**

```powershell
Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force
Get-AzResourceGroup -SubscriptionId $sub -Name $rg -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $source, $blobDownload, $fileDownload -ErrorAction SilentlyContinue
```
