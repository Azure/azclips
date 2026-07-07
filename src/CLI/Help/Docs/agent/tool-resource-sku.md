---
summary: Given a VM SKU, report which Azure regions currently expose that SKU for the current subscription.
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool resource sku` wraps the agent's `find_available_locations` tool. Given a VM SKU (for example `Standard_D2s_v5`), it probes a common set of Azure regions in parallel via `providers/Microsoft.Compute/skus` and returns a JSON list showing which locations have the SKU available, which are blocked at the location level, and which zones inside each location expose it. Backs the agent's automatic recovery from `SkuNotAvailable` errors on `vm create`.

### Check where Standard_D2s_v5 is available

```bash
az agent tool resource sku --vm-size Standard_D2s_v5
```

### Check availability for a specific subscription

```bash
az agent tool resource sku --vm-size Standard_D2s_v5 --subscription-id 00000000-0000-0000-0000-000000000000
```
