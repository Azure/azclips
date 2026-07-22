---
summary: Manage tags in an Azure Container Registry repository.
status: ga
schema: 1.0.0
online version:
---

Description: List, inspect, and delete tags in an Azure Container Registry repository. Tags are mutable references to manifests, providing human-readable names for container images.

### List tags in a repository

```bash
az acr tag list --registry myregistry --repository myrepo
```

### Show tag details

```bash
az acr tag show --registry myregistry --repository myrepo --tag latest
```

### Delete a tag

```bash
az acr tag delete --registry myregistry --repository myrepo --tag latest
```
