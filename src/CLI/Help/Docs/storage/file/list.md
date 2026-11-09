---
summary: List files and directories in an Azure Files directory.
status: preview
schema: 1.0.0
online version:
---

Description: Omit `--path` to list the share root. The result includes both file and directory entries. Explicit marker/limit and extended-information controls preserve continuation behavior. Requires explicit Files backup intent.

### List a directory

```bash
az storage file list --account-name myaccount --share-name reports --path monthly --enable-file-backup-request-intent
```
