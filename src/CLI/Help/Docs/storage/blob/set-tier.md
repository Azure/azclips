---
summary: Set a blob's access tier.
status: preview
schema: 1.0.0
online version:
---

Description: Set the access tier of a block blob or page blob using Microsoft Entra authentication.

### Set a blob tier

```bash
az storage blob set-tier --account-name mystorage --container-name files --name archive.zip --tier Cool
```

### Rehydrate an archived blob quickly

```bash
az storage blob set-tier --account-name mystorage --container-name files --name archive.zip --tier Hot --rehydrate-priority High
```
