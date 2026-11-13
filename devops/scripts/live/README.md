# Live recording refresh

The `Refresh recordings` workflow replays every scenario on a schedule, re-records only the
ones that fail, and opens a pull request with the rewritten files. Playback that still passes
proves nothing about today's Azure; this catches recordings that have gone stale.

`refresh_recordings.py` holds all of the logic and runs the same from a developer machine:

```
python3 devops/scripts/live/refresh_recordings.py --playback-only
```

reports which scenario classes are stale without touching Azure. Without the flag the script
re-records the failing classes, which needs a signed-in profile with a subscription, then
replays them and runs the secret scan over the rewritten files. The run fails if anything
still fails after a re-record, if a failing test class has no recording to refresh, or if the
scan finds anything; the pull request is never opened over scan findings.

If a runner error interrupts re-recording, the script still scans partial recording changes
and writes a summary identifying the incomplete refresh, while keeping the error fatal.
The workflow commits and pushes only after a completed, clean scan and written summary.
Missing or failed scans prevent publishing, including when the refresh step fails.

## Runner

The workflow needs a self-hosted runner labeled `live-recorder` on a VM with a system-assigned
managed identity that has Contributor on the test subscription; managed identity is the only
non-interactive login azclips supports. The repository variable `LIVE_TEST_SUBSCRIPTION_ID`
holds the subscription id, since managed identity login requires it explicitly.

On the GitHub side the workflow runs on the automatic token, with two settings required:
workflow permissions allowed to write, and Allow GitHub Actions to create and approve pull
requests enabled for the repository. A pull request opened with the automatic token does not
trigger its own checks, so store a fine-grained PAT with contents and pull requests write as
the `LIVE_REFRESH_TOKEN` secret; the workflow prefers it when present and the refresh PR then
runs CI like any other.

Provisioning that worked for the spike, task 833: Ubuntu 22.04, Standard_D2s_v3, system-assigned
identity, Contributor role assignment at subscription scope. On the VM install git, python3,
pwsh, and the .NET SDK version `global.json` pins, then register the runner with the
`live-recorder` label. The build and login happen per run in the workflow.

Record mode creates real resources; scenarios delete what they create, and resource naming and
cleanup for interrupted runs is tracked separately in the live tests epic.
