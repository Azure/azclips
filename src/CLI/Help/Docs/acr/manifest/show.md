---
summary: Show details for a manifest.
status: ga
schema: 1.0.0
online version:
---

Description: Retrieve attributes for a specific manifest identified by its digest, including image size, architecture, OS, media type, tags, and changeable attributes.

### Show manifest by digest

```bash
az acr manifest show --registry myregistry --repository myrepo --reference sha256:abc123
```
