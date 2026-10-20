---
summary: Download a public certificate as PEM or DER.
status: preview
schema: 1.0.0
online version:
---

Description: Write the latest or specified version's public X509 certificate to a new file. Requires certificates/get. `--encoding` accepts PEM (default) or DER. Existing destinations are never overwritten. Validates the certificate before file creation. Never reads a backing secret or exports a private key.

### Download DER

```bash
az keyvault certificate download --vault-name myvault --name app-tls --file ./app.cer --encoding DER
```