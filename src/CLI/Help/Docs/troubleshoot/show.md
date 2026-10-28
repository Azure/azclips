---
summary: Show the complete saved command and error locally.
status: preview
schema: 1.0.0
online version:
---

Description: Read one saved failure by its exact ID without AI. Output has id, command, and errorDetail fields. Unlike list, show preserves the full original command and error text. It does not change or clean up history.

### Show the complete failure

```bash
az troubleshoot show --id $failureId --output table
```

### Read the saved error with a query

```bash
az troubleshoot show --id $failureId --output json --query errorDetail
```

### Notes

Set $failureId to a complete ID from a failure hint or az troubleshoot list. Missing, invalid, or evicted IDs never select another record. An unavailable key, incompatible draft format, corrupt data, or incomplete cleanup is reported as an error rather than an empty result.
