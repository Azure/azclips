---
summary: Update secret attributes without changing its value.
status: preview
schema: 1.0.0
online version:
---

Description: Update the latest or specified version's enabled state, expiration, not-before time, content type or tags. Supply at least one update. Omitted properties remain unchanged; explicit `--enabled false` is preserved. Requires secrets/set permission. Tags supplied here replace the existing tag collection. Output contains metadata only. Use `set` to create a new secret value/version.

### Disable a secret

```bash
az keyvault secret set-attributes --vault-name myvault --name app-password --enabled false
```

### Update expiry

```bash
az keyvault secret set-attributes --vault-name myvault --name app-password --expires 2027-01-01T00:00:00Z
```