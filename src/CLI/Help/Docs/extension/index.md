---
summary: Manage Cirrus extension packages and discovery-backed extension state.
status: ga
schema: 1.0.0
online version:
---

Description: Install, list, show, update, and remove extension packages that extend Cirrus functionality. Read-side commands are discovery-backed: installed payloads are discovered from disk at runtime, while the registry stores user preferences such as pin state.

### Install an extension from a feed

```bash
az extension add --name MyExtension --version 1.0.0 --feed-url https://packages.example.com/nuget
```

### Install an extension from a local file

```bash
az extension add --path ./MyExtension.1.0.0.nupkg
```
