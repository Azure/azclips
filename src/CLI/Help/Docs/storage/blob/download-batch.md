---
summary: Download blobs from a container to a directory.
status: preview
schema: 1.0.0
online version:
---

Description: Download blobs to a local directory using Microsoft Entra authentication.

### Download matching blobs

```bash
az storage blob download-batch --account-name mystorage --source files --destination ./reports --pattern "*.csv"
```
