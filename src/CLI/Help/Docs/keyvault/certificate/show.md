---
summary: Read a public certificate and metadata.
status: preview
schema: 1.0.0
online version:
---

Description: Get the latest certificate or an explicit `--version`. Requires certificates/get. Returns public bytes, policy, attributes and references. Does not follow backing secret/key references or export a private key.

### Show latest

```bash
az keyvault certificate show --vault-name myvault --name app-tls
```