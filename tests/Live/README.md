# Live tests

Everything needed to run the managed-identity tests against **real Azure**: the test
project, the Terraform that provisions the VM, and the scripts that drive it.

```
tests/Live/
├── Live.Tests/   xUnit v3 assertions; published self-contained and run ON the VM
├── Terraform/    bootstrap (CI identity) + linux-vm (the VM under test)
└── Scripts/      build the bundle, ship it to the VM, run the suite
```

Self-contained on purpose: nothing outside this directory depends on it, so it can be
read, moved, or removed as a unit.

## Where this sits in the test pyramid

Live tests are the **most expensive and least deterministic** layer, so they should
only cover what cheaper layers physically cannot.

| Layer | Azure | Credentials | Gates PRs | Cost |
| ----- | ----- | ----------- | --------- | ---- |
| `tests/Cli.Tests` (incl. `ManagedIdentityE2ETests`) | mock IMDS, in-process | none | yes | ~0 |
| `tests/Scenario.Tests` | recorded HTTP, replayed | none | yes | ~0 |
| `tests/Live` (here) | **real** IMDS + ARM | real managed identity | no, manual | minutes + VM billing |

Before adding a case here, check it cannot live in `ManagedIdentityE2ETests`, which
already spawns the real `az` binary against a fake IMDS and asserts on the profile it
writes. Only these justify the live tier:

- the **real** IMDS contract, and real AAD/RBAC decisions (including that a wrong
  identity is genuinely *rejected*, not rejected by our mock)
- identity selectors whose behaviour is IMDS-side (`--object-id`, `--resource-id`)
- the **self-contained bundle running on a clean VM** with no SDK and no developer
  machine state

## Running

Live tests are skipped unless `AZCLIPS_LIVE_TEST=1`, so a plain `dotnet test` stays
green with no Azure access. The orchestrator sets that up for you.

```bash
dotnet run --project tests/Live/Tool                    # normal run
dotnet run --project tests/Live/Tool -- --skip-publish  # reuse the tarball
dotnet run --project tests/Live/Tool -- --provision     # plan/apply the VM first
```

The orchestrator is C# (`tests/Live/Tool`) rather than bash so it runs natively on
Windows and macOS as well as Linux — the previous shell version depended on GNU-only
behaviour (`date +%s%N`, `grep -oP`, `sha256sum`, bash 4 features). Everything that
executes *inside* the VM (`Scripts/setup-vm-mi.sh`, `Scripts/lib/timing.sh`) stays
bash, because that is where it runs.

`--skip-publish` reuses the existing bundle. After editing C# you must **not** pass it,
or you will silently test stale code.

In CI the same tool runs from `.github/workflows/mi-live.yml` (manual dispatch only).
Only one `az vm run-command` can target a VM at a time, so do not run locally while CI
is running.

First-time setup, the VM lifecycle, and cost/teardown are documented in
[`Terraform/linux-vm/README.md`](Terraform/linux-vm/README.md).
