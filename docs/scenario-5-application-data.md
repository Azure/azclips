# Scenario 5: Store and retrieve application data

**Customer goal:** "I want my application to store a file in Azure, retrieve the same data and remove it when it is no longer needed."

**Successful outcome:** Blob and file-share round trips preserve the test file's contents, and all run-owned data and storage resources are removed.

## Prerequisites

- AzCLIPS CLI and preview PowerShell modules installed and authenticated in PowerShell 7.6+.
- Approved storage authentication and network access for both blob and file-share tests.

## Shared inputs

Commands use PowerShell syntax. Stop on unexpected errors; check `$LASTEXITCODE` after CLI commands. Storage capabilities remain **Pending**; do not substitute classic storage commands.

```powershell
$ErrorActionPreference = 'Stop'
$az = '<full-path-to-AzCLIPS-executable>'
$sub = '<test-subscription-id>'
$location = '<approved-region>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-data-$run"
$account = "clips$([guid]::NewGuid().ToString('N').Substring(0, 18))"
$container = "data-$run"
$share = "files-$run"
$source = Join-Path $env:TEMP "$rg-source.txt"
$blobDownload = Join-Path $env:TEMP "$rg-blob.txt"
$fileDownload = Join-Path $env:TEMP "$rg-file.txt"
```

## Customer journey

CLI and PowerShell are alternative execution paths. Use only synthetic data, a private container and a dedicated storage account. Blob and file-share authentication must be confirmed independently.

| Step | Customer action | Commands (AzCLIPS CLI & AzCLIPS PowerShell) | Expected result / verification |
|---|---|---|---|
| **1. Create the data environment** | Create a dedicated group, storage account, private blob container and file share. | **AzCLIPS CLI:**<br>`& $az group create --subscription-id $sub --name $rg --location $location`<br><br>**AzCLIPS PowerShell:**<br>`New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location`<br><br>**Pending, both surfaces:** account create/show/list/update, container create and file-share create commands; authentication parameters. | Account, private container and share exist in the intended context. Confirm approved access settings and an account SKU supporting the chosen services. Account creation alone is not data-plane success. |
| **2. Upload application data** | Create a local test file and upload it as a blob. List it and inspect metadata. | **Workstation command:**<br>`Set-Content -LiteralPath $source -Value "AzCLIPS data round trip $run" -NoNewline`<br><br>**Pending, AzCLIPS CLI and PowerShell:** blob upload/list/show commands using `$account`, `$container`, `$source` and blob name `payload.txt`. | Exactly the intended blob exists with the expected byte length. No account key, SAS or token is displayed or retained in evidence. |
| **3. Retrieve and change the blob** | Download the blob, verify its contents, then replace it with an updated payload and verify again. | **Pending, both surfaces:** blob download and overwrite/update commands, writing to `$blobDownload`.<br><br>**Workstation verification:**<br>`if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $blobDownload).Hash) { throw 'Blob content mismatch.' }`<br><br>Change the source with:<br>`Set-Content -LiteralPath $source -Value "AzCLIPS updated payload $run" -NoNewline`<br>Repeat upload, download and hash verification. | Both round trips match the current source file. A missing download, stale download or hash mismatch is a failure, not a passed command. |
| **4. Share a file** | Upload the same payload to the file share, list it, download it and verify the bytes. | **Pending, AzCLIPS CLI and PowerShell:** file upload/list/download commands using `$account`, `$share`, `$source` and destination `$fileDownload`.<br><br>**Workstation verification:**<br>`if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $fileDownload).Hash) { throw 'File-share content mismatch.' }`<br><br>**Pending variant:** retry a read using an approved identity without data access. | File round trip matches the source. The denied-access variant fails for the intended authorization reason, not because of DNS, network or missing-resource errors. |
| **5. Remove data and resources** | Delete the uploaded data, container, share and storage account; then remove the group and local files. | **Pending, both surfaces:** blob/file delete, container/share delete and account delete commands. Verify object absence before deleting the account.<br><br>**AzCLIPS CLI:**<br>`& $az group delete --subscription-id $sub --name $rg`<br>`& $az group exists --subscription-id $sub --name $rg`<br><br>**AzCLIPS PowerShell:**<br>`Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force`<br>`Get-AzResourceGroup -SubscriptionId $sub -Name $rg`<br><br>**Workstation:** remove only `$source`, `$blobDownload` and `$fileDownload` if created. | No active test objects, storage account or group remains. Retention/soft-delete behavior is recorded separately; no purge is required. Group deletion remains the resource cleanup path if individual Pending delete commands are unavailable. |
