---
summary: Manage repositories in an Azure Container Registry.
status: ga
schema: 1.0.0
online version:
---

Description: List, inspect, and delete repositories in an Azure Container Registry. Repositories group related container images and artifacts under a common name.

### List all repositories

```bash
az acr repository list --registry myregistry
```

### Show repository details

```bash
az acr repository show --registry myregistry --repository myrepo
```

### Delete a repository

```bash
az acr repository delete --registry myregistry --repository myrepo
```
