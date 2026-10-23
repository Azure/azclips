---
summary: Return the selected Storage account's ARM key records.
status: preview
schema: 1.0.0
online version:
---

Description: Requires permission to list account keys and a resource group/account/subscription. The subscription can come from the selected profile. The returned keys collection includes secret values. Permission casing is preserved from the service. Use `--expand-key-type kerb` only when Kerberos keys are required.

### List keys without changing data-plane authentication

```bash
az storage account keys list --resource-group source-rg --account-name sourceaccount
```
