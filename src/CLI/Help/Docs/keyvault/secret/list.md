---
summary: List secret metadata across all pages.
status: preview
schema: 1.0.0
online version:
---

Description: List base secret identifiers and attributes without retrieving values. Requires secrets/list permission. `--maxresults` sets the page size between 1 and 25, not the total result limit. Continuation requests stay within the selected vault.

### List secrets

```bash
az keyvault secret list --vault-name myvault
```