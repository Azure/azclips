---
summary: List certificate metadata across all pages.
status: preview
schema: 1.0.0
online version:
---

Description: Requires certificates/list. `--maxresults` is page size from 1 to 25, not total count. Omit it for the service default. `--include-pending true` includes incomplete certificates; explicit false is preserved. Cross-vault continuation links are rejected.

### List certificates

```bash
az keyvault certificate list --vault-name myvault --maxresults 25 --include-pending true
```