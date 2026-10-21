---
summary: Manage Key Vault certificates.
status: preview
schema: 1.0.0
online version:
---

Description: Create, import, read, list, update, download and soft-delete certificates in an existing public-cloud vault. Use `--vault-name` or a root HTTPS `--vault-url`. Policy JSON accepts classic CLI and REST API 2025-07-01 field names. Results use classic CLI certificate, policy and date field names. Download returns public material only. Issuer management, pending-CSR merge, private-key export, recovery and purge are not included.

### List certificate metadata

```bash
az keyvault certificate list --vault-name myvault
```