---
summary: Remove the pin from an extension so it can be updated again.
status: ga
schema: 1.0.0
online version:
---

Description: Clear the pin flag on a previously-pinned extension. After unpinning, `azv2 extension update` will once again consider the extension for automatic upgrades.

### Unpin by verb

```bash
azv2 extension unpin --name compute
```
