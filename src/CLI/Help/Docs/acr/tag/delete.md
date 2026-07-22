---
summary: Delete a tag from a repository.
status: ga
schema: 1.0.0
online version:
---

Description: Remove the specified tag from a repository. Only the tag reference is deleted; the underlying manifest is preserved unless no other tags reference it.

### Delete a tag

```bash
az acr tag delete --registry myregistry --repository myrepo --tag latest
```
