---
summary: Retrieve secret metadata or explicitly retrieve its value.
status: preview
schema: 1.0.0
online version:
---

Description: Get the latest version, or select one with `--version`. Requires secrets/get permission. Output has no secret-value property unless `--as-plain-text` is supplied; that option returns the value directly. Plaintext output can be captured by the terminal or calling process, so use it only when needed.

### Get metadata

```bash
az keyvault secret show --vault-name myvault --name app-password
```

### Retrieve a value explicitly

```bash
az keyvault secret show --vault-name myvault --name app-password --as-plain-text
```