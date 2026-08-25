---
summary: Delete a manifest from a repository.
status: ga
schema: 1.0.0
online version:
---

Description: Delete the specified manifest by digest from a repository. All tags pointing to this manifest are also removed. This action is irreversible.

### Delete a manifest by digest

```bash
az acr manifest delete --registry myregistry --repository myrepo --reference sha256:abc123
```
