---
summary: List all versions of a certificate.
status: preview
schema: 1.0.0
online version:
---

Description: Enumerate version metadata across all pages. Requires certificates/list. `--maxresults` is page size from 1 to 25, not total limit. No private key or backing secret is retrieved.

### List versions

```bash
az keyvault certificate list-versions --vault-name myvault --name app-tls --maxresults 1
```