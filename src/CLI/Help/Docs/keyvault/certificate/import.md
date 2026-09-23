---
summary: Import a PFX or PEM certificate with its private key.
status: preview
schema: 1.0.0
online version:
---

Description: Import a PFX/PKCS#12 file or PEM containing a certificate and PKCS#8 private key. Requires certificates/import. Files are limited to 1 MiB. `--password-file` must contain the exact UTF-8 password with no extra newline. Plaintext password arguments are not exposed. Protect and remove input files according to local policy. Request payloads/passwords are excluded from diagnostics. Optional policy content type must match the file. Public-only imports and pending-CSR merge are not supported. Output never contains the private key or password.

### Import encrypted PFX

```bash
az keyvault certificate import --vault-name myvault --name app-tls --file ./app.pfx --password-file ./password.txt
```