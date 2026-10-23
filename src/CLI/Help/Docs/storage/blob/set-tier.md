---
summary: Set a supported Block or Page Blob access tier.
status: preview
schema: 1.0.0
online version:
---

Description: Set a compatible tier without changing blob type. Block is the default type; select `--type page` for supported premium Page tiers. Block rehydration can specify `--rehydrate-priority`.

### Move a Block Blob to Cool

```bash
az storage blob set-tier --account-name myaccount --container-name reports --name report.csv --tier Cool
```
