---
summary: List Blob entries with explicit paging and hierarchy controls.
status: preview
schema: 1.0.0
online version:
---

Description: Use a literal prefix and optional delimiter. `--num-results` defaults to 5000; `*` drains all pages. Empty pages with continuation are followed. `--show-next-marker` appends a marker object, including null at completion. Reuse returned markers only with their original listing inputs.

### List a prefix and expose continuation

```bash
az storage blob list --account-name myaccount --container-name reports --prefix monthly/ --show-next-marker
```
