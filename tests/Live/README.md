# Live tests

Everything needed to run the managed-identity tests against **real Azure**: the test
project, the Terraform that provisions the VM, and the tool that drives it.

```
tests/Live/
├── Live.Tests/   xUnit v3 assertions; published self-contained and run ON the VM
├── Terraform/    bootstrap (CI identity) + linux-vm (the VM under test)
├── Tool/         the orchestrator: builds the bundle, ships it, runs the suite
├── Tool.Tests/   unit tests for the orchestrator (no Azure; run by plain `dotnet test`)
└── Scripts/      the parts that execute INSIDE the VM, so they stay bash
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

**First time?** You need an Azure VM of your own before any of this works. Follow
[`Terraform/linux-vm/README.md`](Terraform/linux-vm/README.md) — sign in, choose an
alias, then run stage 0 (once per repo) and stage 1 (creates the VM).

### Locally

Run from the repository root, on Linux, macOS or Windows:

```bash
dotnet run --project tests/Live/Tool                    # normal run
dotnet run --project tests/Live/Tool -- --provision     # plan/apply the VM first
dotnet run --project tests/Live/Tool -- --skip-publish  # reuse the tarball
```

Prerequisites are `terraform`, the .NET SDK, and `az` already signed in to the right
tenant. Use `--provision` after changing the Terraform, or if the VM has been destroyed;
it is a fast no-op when nothing has changed. A successful run ends with
`End-to-end MI test PASSED.`

`--help` lists the remaining options. Once you are done for the day, destroy the VM —
it bills while it exists, and nothing shuts it down for you:

```bash
cd tests/Live/Terraform/linux-vm && terraform destroy
```

### In your own CI

The workflow is `.github/workflows/mi-live.yml`, manual dispatch only. It runs this
same tool against **your** resources, so it needs stage 0 to have been run and the
repository variable `ALIAS` set to the same alias as your local `terraform.tfvars` —
otherwise CI provisions a second, parallel set of infrastructure.

```bash
gh workflow run mi-live.yml --repo "<you>/<fork>" --ref "$(git branch --show-current)"
gh run watch "$(gh run list --repo "<you>/<fork>" --workflow mi-live.yml \
  --limit 1 --json databaseId --jq '.[0].databaseId')" --repo "<you>/<fork>"
```

Dispatch against your fork explicitly: the upstream repository has no such workflow
and returns a bare 404. Any branch works — the OIDC credential is scoped to the `dev`
environment, not to a branch.

Full setup, website instructions and troubleshooting:
[`Terraform/linux-vm/README.md`](Terraform/linux-vm/README.md#run-the-github-actions-pipeline-manually).

The orchestrator is C# (`tests/Live/Tool`) rather than bash so it runs natively on
Windows and macOS as well as Linux — the previous shell version depended on GNU-only
behaviour (`date +%s%N`, `grep -oP`, `sha256sum`, bash 4 features). Everything that
executes *inside* the VM (`Scripts/setup-vm-mi.sh`, `Scripts/lib/timing.sh`) stays
bash, because that is where it runs.

`--skip-publish` reuses the existing bundle. After editing C# you must **not** pass it,
or you will silently test stale code.

Local and CI runs share the same VM, and Azure allows only one `az vm run-command`
against it at a time. The second caller is rejected outright:

```
(Conflict) Run command extension execution is in progress.
Please wait for completion before invoking a run command.
```

Nothing is corrupted — the runs cannot interleave, and each uploads a
timestamped blob of its own — but the loser fails only at the final phase, after
having already built and uploaded its bundle. Which side loses is whichever
arrives second, so a local run will fail CI just as readily as the reverse.

First-time setup, the VM lifecycle, and cost/teardown are documented in
[`Terraform/linux-vm/README.md`](Terraform/linux-vm/README.md).
