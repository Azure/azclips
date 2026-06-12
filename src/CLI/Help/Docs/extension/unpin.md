---
summary: Remove the pin from an extension so it can be updated again.
status: ga
schema: 1.0.0
online version:
---

Description: Clear the pin flag on a previously-pinned extension. After unpinning, `azv2 extension update` will once again consider the extension for automatic upgrades. The extension is identified by its package id (full or short suffix); verb and namespace tokens are not accepted as selectors.

### Unpin by short name

```bash
azv2 extension unpin --name Compute
```
