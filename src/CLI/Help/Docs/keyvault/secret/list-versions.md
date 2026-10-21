---
summary: List every version of a secret without its values.
status: preview
schema: 1.0.0
online version:
---

Description: Return version identifiers and metadata across all pages. Requires secrets/list permission. `--maxresults` controls page size from 1 to 25, not total results.

### List versions

```bash
az keyvault secret list-versions --vault-name myvault --name app-password
```