---
summary: Show version information.
status: ga
schema: 1.0.0
online version:
---

Description: Display version information for the Azure CLI, including the CLI version, the .NET runtime, and the install, configuration, and extension directories. Use `az version` for structured JSON output or `az --version` for a human-readable summary.

### Show structured version information

```bash
az version
```

### Show version information as plain text

```bash
az --version
```

### Notes

Replace `<your-format>` with a supported output format, then run `az version` --output json to render it.
