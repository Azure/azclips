---
summary: Create a certificate version from an explicit policy.
status: preview
schema: 1.0.0
online version:
---

Description: Start issuance using exactly one of `--policy` (REST-policy JSON) or `--policy-file` (UTF-8 JSON). Requires certificates/create and certificates/get when waiting. Known issuers are polled at the named pending endpoint. Default timeout is 300 seconds; `--timeout` accepts 1-3600 seconds. `--no-wait` and issuer `Unknown` return pending immediately. Returns a certificate operation, not a certificate bundle. Local timeout or cancellation does not cancel server issuance. External signing/CSR merge requires another tool.

### Create a self-signed certificate

```bash
az keyvault certificate create --vault-name myvault --name app-tls --policy '{"issuer":{"name":"Self"},"key_props":{"kty":"RSA","key_size":2048,"exportable":false},"secret_props":{"contentType":"application/x-pkcs12"},"x509_props":{"subject":"CN=app.example","validity_months":12}}'
```

### Start issuance without waiting

```bash
az keyvault certificate create --vault-name myvault --name app-tls --policy-file ./policy.json --no-wait
```

Optional enabled state, dates and tags map only supplied values. Dates are supported through January 2038. Policy files are limited to 1 MiB.