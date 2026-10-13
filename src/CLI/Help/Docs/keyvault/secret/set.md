---
summary: Set a secret or create a new version.
status: preview
schema: 1.0.0
online version:
---

Description: Set a UTF-8 secret using exactly one of `--file` or `--value`. Prefer a protected file to avoid putting the value in shell history or process arguments. File contents, including trailing newlines, are preserved. Values must not exceed 25 KiB. Requires secrets/set permission. The response contains metadata, not the value. No vault is created.

### Set from a file

```bash
az keyvault secret set --vault-name myvault --name app-password --file ./secret.txt
```

### Set with attributes

```bash
az keyvault secret set --vault-name myvault --name app-password --file ./secret.txt --enabled false --expires 2027-01-01T00:00:00Z --tags owner=app
```

Dates use the selected API's Unix timestamp representation, supported here through January 2038. Protect and remove local secret files according to your environment's policy.