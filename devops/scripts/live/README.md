# Live recording refresh

The `azclips-refresh-recordings` pipeline (`.azure-pipelines/azclips-refresh-recordings.yml`)
replays every scenario weekly, re-records only the ones that fail, and opens a pull request
with the rewritten files. Playback that still passes proves nothing about today's Azure; this
catches recordings that have gone stale.

It runs in the internal `release` Azure DevOps project because recording needs credentials
for a live test subscription. It used to be a GitHub Actions workflow on a self-hosted
runner; a credentialed runner reachable from a public repository's workflows is a risk, so
it moved inside the Azure DevOps trust boundary.

`refresh_recordings.py` holds all of the logic and runs the same from a developer machine:

```
python3 devops/scripts/live/refresh_recordings.py --playback-only
```

reports which scenario classes are stale without touching Azure. Without the flag the script
re-records the failing classes, which needs a signed-in profile with a subscription, then
replays them and runs the secret scan over the rewritten files. A class that still fails after
its re-record gets its committed recordings back, so only recordings that replayed remain.

The exit code separates the outcomes:

| Code | Meaning | Published |
| --- | --- | --- |
| 0 | Nothing failed | Rewritten recordings, if any |
| 3 | Some classes are still stale, or failed without a recording | The recordings that refreshed and replayed |
| 4 | The secret scan has findings | Nothing |
| other | Unexpected error | Nothing |

## Pipeline

Runs Mondays 06:00 UTC on `main` (`always: true`: recordings go stale when Azure changes, not
when the repository does), and on manual queue with an optional `scenario_filter`: only scenario
classes whose name contains it, letters, digits, `_` and `.` only. Every run checks out `main`,
builds, signs the built CLI in, and runs the script with `AZCLIPS_TEST_RUN_ID` set to the build
id, so the `-t<8hex>` suffix on any resource group left behind by an interrupted run maps back
to the run that created it.

When the script exits 0 or 3 and recordings changed, the pipeline fetches the GitHub token from
Key Vault (`get-github-pat-token` group), pushes `live/recording-refresh-<build id>`, and opens
a pull request with the run summary as its body. The pull request is opened with that token
rather than a workflow token, so its normal validation runs. A run with nothing rewritten opens
nothing. Code 3 or 4 fails the run either way, and the summary appears on the run's Extensions
tab. Rewritten recordings are never published as build artifacts.

Sign-in uses an Azure Resource Manager service connection with workload identity federation
(`live_test_service_connection` in the pipeline). The `AzureCLI@2` task exposes the connection's
federated token, and the built CLI signs in with
`az login --service-principal --federated-token <token> --client-id <id> --tenant-id <id>
--subscription-id <id>`, using the connection's subscription. azclips keeps the federated token
and presents it again whenever its access token needs renewing, so a run must finish its record
work while that federated token is still accepted.

## Azure DevOps setup

None of this is enforced by the YAML; the in-pipeline checks (release project, `main` only,
input validation) are defense in depth.

- Service connection `azclips-live-recording`: Azure Resource Manager, workload identity
  federation, scoped to the test subscription, Contributor on that subscription only. Grant
  access to this pipeline alone (not all pipelines), and add a Branch control check that allows
  only `refs/heads/main`. Add an Exclusive lock check so two runs never record at once.
- Pipeline permissions: only the owning team may queue or edit `azclips-refresh-recordings`.
- Pool: the shared `ubuntu_pool`. The pipeline needs no identity on the pool; the subscription
  credential comes only from the service connection, inside the sign-in task. Never give a
  shared pool a managed identity with access to the test subscription: every job on that pool
  would hold it, which is the problem this pipeline exists to avoid.
- GitHub token: the Key Vault secret named by `GitHubPatSecretName` must be able to push a
  branch to `Azure/azclips` and open a pull request. That secret is the release token, which
  can also push tags; a token limited to branches and pull requests is preferable.

## Ownership

To be confirmed. Proposed: Platform Engineering owns the pipeline and its Azure DevOps
resources; Quality & Productivity owns the scenario and recording behavior in
`refresh_recordings.py`.

Record mode creates real resources; scenarios delete what they create, and resource naming and
cleanup for interrupted runs is tracked separately in the live tests epic.
