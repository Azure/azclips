---
summary: Synchronously refresh the on-disk ARM cache used by agent tool resource find/tree/show.
status: ga
schema: 1.0.0
online version:
---

Description: `az agent tool resource refresh` rebuilds the on-disk cache under `.azclips/agent/arm/<tenantId>/` in the local application data folder (`%LOCALAPPDATA%` on Windows) and waits for it to finish before returning. The other resource commands (`find`, `tree`, `show`) kick a background refresh and answer from whatever is on disk — on a cold machine that's empty. Use this command once at setup, then again whenever the cache looks stale (e.g. you just created resources and they are missing from `find` results).

### First-time setup

```bash
az agent tool resource refresh
```

Typical output when the tenant has ~15k resources:

```json
{"status":"ok","tenantId":"72f988bf-86f1-41af-91ab-2d7cd011db47","elapsedSeconds":68.4}
```

### After creating new resources you want to query immediately

```bash
az group create --name my-new-rg --location eastus --subscription-id 00000000-0000-0000-0000-000000000000
az agent tool resource refresh
az agent tool resource find --name my-new-rg --kind resourceGroup
```

### Notes

- Auth: requires `az login`; the refresh reads the active profile's tenant.
- TTLs: tenants and subscriptions refresh only when missing; resource groups have a 1-day TTL; resources have an 8-hour TTL. This command triggers a refresh for each cache file whose TTL has expired (or that is missing).
- Cross-process safe: if another `az agent tool resource refresh` is running for the same tenant, this call waits briefly for the file lock (up to 10 seconds per cache file) before bowing out. Concurrent invocations therefore add tens of seconds in the worst case, not a full refresh's runtime.
