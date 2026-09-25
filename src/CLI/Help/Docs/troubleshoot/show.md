---
summary: Show the complete saved command and error locally.
status: preview
schema: 1.0.0
online version:
---

Description: Read one saved failure by its exact ID without AI, Azure authentication, or network access. Output has id, command, and errorDetail fields. Unlike --list, show preserves the full original command and error text, including line breaks, even with table output in a narrow terminal. It does not change or clean up history.

### Show the complete failure

```bash
az troubleshoot show --id $failureId --output table
```

### Read the saved error with a query

```bash
az troubleshoot show --id $failureId --output json --query errorDetail
```

### Notes

Set $failureId to a complete ID from a failure hint or az troubleshoot --list. Tab completion works after --id and --id=. Missing, invalid, or evicted IDs never select another record. --style and --list cannot be used with show.

Tab reads only the ID/timestamp index and does not verify decryption. Show checks the full record when reading it. An unavailable key, incompatible draft format, corrupt data, or incomplete cleanup is reported as an error rather than an empty result.
