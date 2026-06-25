---
summary: Remove an installed Cirrus extension package.
status: ga
schema: 1.0.0
online version:
---

Description: Uninstall an extension package by deleting its installed payload and removing any persisted preference entry. The extension is identified by its package id (full or short suffix). Verb and namespace tokens are not accepted; a missed lookup includes a `Did you mean '--name <Package>'?` hint when the token unambiguously maps to an installed package. Built-in extensions cannot be removed.

### Remove by short name

```bash
az extension remove --name Compute
```

### Remove by package id

```bash
az extension remove --name Microsoft.Azure.Cirrus.Extensions.Compute
```
