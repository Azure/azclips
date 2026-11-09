---
summary: Acknowledge a normal asynchronous Blob copy.
status: preview
schema: 1.0.0
online version:
---

Description: Resolve source and destination independently. An unsigned Blob source in the destination account uses the destination's Entra authorization when both select the same profile, without requesting a user delegation key. Other unsigned Blob sources use source-profile user delegation. Files sources require a service/account SAS, `--source-account-key`, or explicit `--use-source-account-key-lookup` with `--source-key-resource-group`. A supplied SAS takes precedence; failed SAS never falls back to keys. Key signing needs Shared Key enabled only on the source account. Destination requests remain Entra-authenticated.

Copy overwrites by default. Use `--destination-if-none-match "*"` for create-only protection. Output contains `copyId`, `copyStatus`, `etag` and `lastModified`; pending is not completed content. No implicit polling, synchronous-copy requirement or blob-type conversion is supported. See the [Storage overview](../../index.md) for all three Files-source authorization examples and key-lookup permissions/defaults.

### Start a Blob-source copy

```bash
az storage blob copy start --account-name destaccount --destination-container archive --destination-blob copied.csv --source-account-name sourceaccount --source-container reports --source-blob original.csv
```

### Explicitly authorize Files-source ARM key lookup

```bash
az storage blob copy start --account-name destaccount --destination-container archive --destination-blob copied.csv --source-account-name sourceaccount --source-share reports --source-path original.csv --use-source-account-key-lookup --source-key-resource-group source-rg
```
