## Decisions

- Fetch release metadata and the selected checksum-verified bundle once on a hosted agent with authenticated GitHub CLI. Share the prepared baseline as a pipeline artifact; never put GitHub credentials in distribution containers.
- Compare the Az version encoded by the PowerShell bundle, not timestamped Git tags. Equal latest/target versions select the highest older distinct stable version. Reject downgrades and absence of an older baseline.
- Use the released bundle's InstallModule.ps1 with explicit versions and a unique repository name. Copy pipeline packages into that same writable gallery. Install each target module through the helper, then the rollup, so unchanged dependency versions cannot retain released payloads.
- Match the existing fresh-install CI policy for unsigned pipeline inputs: target workers supply Install-Module:SkipPublisherCheck through process-local default parameters, without editing the released helper. Baseline publisher checks remain unchanged; package-signature compatibility is not claimed by this suite.
- Compare all installed package payload files with their package SHA256 hashes, excluding NuGet transport metadata and the generated PSGetModuleInfo.xml. Check exact versions, import paths and expected commands in fresh bounded processes.
- Preflight both baseline and target identities, including unregistered version directories. Reject preexisting exact versions. Cleanup only attempted identities in the preflight module roots, plus this transaction's repository and scratch directory.
- Run Bootstrap, Dependencies, Upgrade, Install, Validate and Uninstall. Upgrade cleans itself before fresh Install and is gated on bootstrap, not successful dependency validation. A failed baseline preparation does not prevent the independent matrix from running.
- Keep matrix-generator self-tests in local/GitHub runs rather than the Azure DevOps validation job. Native Windows entries accept the latest hosted-agent alias; container, PowerShell and .NET versions remain numeric.
- Make Upgrade required for a successful schema-3 phase report and publish upgrade-tests.json with release selection, package provenance, operation outcomes and cleanup errors.

## Scope

This validates the documented GitHub-bundle/PowerShellGet path. Public PSGallery and PSResourceGet remain separate work. Offline fixtures use real packages/files and a strictly isolated fake package manager; they never install real Az modules into the developer's profile.
