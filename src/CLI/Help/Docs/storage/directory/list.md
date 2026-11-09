---
summary: List only directories from an Azure Files listing.
status: preview
schema: 1.0.0
online version:
---

Description: Omit `--name` for the share root. Files are filtered out while continuation is processed according to the selected marker/limit contract. Requires explicit Files backup intent.

### List child directories

```bash
az storage directory list --account-name myaccount --share-name reports --enable-file-backup-request-intent
```
