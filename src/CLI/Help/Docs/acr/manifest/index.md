---
summary: Manage manifests in an Azure Container Registry repository.
status: ga
schema: 1.0.0
online version:
---

Description: List, inspect, and delete manifests in an Azure Container Registry repository. Each manifest represents a container image or artifact layer configuration identified by a digest.

### List manifests in a repository

```bash
az acr manifest list --registry myregistry --repository myrepo
```

### Show manifest details by digest

```bash
az acr manifest show --registry myregistry --repository myrepo --reference sha256:abc123
```

### Delete a manifest by digest

```bash
az acr manifest delete --registry myregistry --repository myrepo --reference sha256:abc123
```
