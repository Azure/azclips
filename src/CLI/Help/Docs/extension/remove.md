---
summary: Remove an installed Cirrus extension.
status: ga
schema: 1.0.0
online version:
---

Description: Uninstall an extension from the Cirrus extension registry. The extension is identified by its package id (full or short suffix). Verb and namespace tokens are not accepted; a missed lookup includes a `Did you mean '--name <Package>'?` hint when the token unambiguously maps to an installed package. Built-in extensions cannot be removed.

### Remove by short name

```bash
azv2 extension remove --name Compute
```

### Remove by package id

```bash
azv2 extension remove --name Microsoft.Azure.Cirrus.Extensions.Compute
```
