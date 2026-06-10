---
summary: Remove an installed Cirrus extension.
status: ga
schema: 1.0.0
online version:
---

Description: Uninstall an extension from the Cirrus extension registry. The extension is identified by its verb or package id. Built-in extensions cannot be removed.

### Remove by verb

```bash
azv2 extension remove --name compute
```

### Remove by package id

```bash
azv2 extension remove --name Microsoft.Azure.Cirrus.Extensions.Compute
```
