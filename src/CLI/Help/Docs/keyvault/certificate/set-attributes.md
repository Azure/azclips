---
summary: Update supplied certificate attributes, tags or policy.
status: preview
schema: 1.0.0
online version:
---

Description: Preserve omitted attributes and explicit false. At least one change is required. `--policy`/`--policy-file` updates the name-wide policy and cannot be combined with `--version`. Policy is sent before attributes; the requests are not transactional. Policy-only update reads the certificate afterward. Requires certificates/update and certificates/get for that follow-up read. Dates are supported through January 2038.

### Disable a certificate

```bash
az keyvault certificate set-attributes --vault-name myvault --name app-tls --enabled false
```

### Update policy

```bash
az keyvault certificate set-attributes --vault-name myvault --name app-tls --policy '{"x509_props":{"validity_months":12}}'
```