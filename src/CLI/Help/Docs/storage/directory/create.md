---
summary: Create one Azure Files directory beneath an existing share.
status: preview
schema: 1.0.0
online version:
---

Description: Create the selected path with optional metadata. Existing directories return `created:false` unless `--fail-on-exist` is selected. Requires explicit Files backup intent and an existing parent; does not create shares.

### Create a directory

```bash
az storage directory create --account-name myaccount --share-name reports --name monthly --enable-file-backup-request-intent
```
