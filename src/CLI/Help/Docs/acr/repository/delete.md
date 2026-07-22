---
summary: Delete a repository from a container registry.
status: ga
schema: 1.0.0
online version:
---

Description: Delete the specified repository and all its manifests and tags from the container registry. This action is irreversible.

### Delete a repository

```bash
az acr repository delete --registry myregistry --repository myrepo
```
