Status: Completed

Commit: f726c3284

Exact test command run:

dotnet test "tests\Cli.Tests\Cli.Tests.csproj" -v minimal

Test output (truncated to relevant section):

Determining projects to restore...
  All projects are up-to-date for restore.
  ...
  Cli.Tests -> C:\Users\jaysontai\Code\copilot-worktrees\cirrus\jaysontaimicrosoft-shiny-lamp\tests\Cli.Tests\bin\Debug\net10.0\Cli.Tests.dll
Test run for C:\Users\jaysontai\Code\copilot-worktrees\cirrus\jaysontaimicrosoft-shiny-lamp\tests\Cli.Tests\bin\Debug\net10.0\Cli.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:06.25]     Cli.Tests.ProgramFallbackTests.Fallback_ShouldUseDefaultFlavor_WhenNotSet [SKIP]
  Skipped Cli.Tests.ProgramFallbackTests.Fallback_ShouldUseDefaultFlavor_WhenNotSet [1 ms]
...
[xUnit.net 00:00:07.14]     Cli.Tests.ManagedIdentityE2ETests.ManagedIdentity_E2E_SystemAssigned_ShouldLoginAndSaveProfile [SKIP]
[xUnit.net 00:00:07.18]     Cli.Tests.ManagedIdentityE2ETests.ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail [FAIL]
[xUnit.net 00:00:07.19]     Cli.Tests.ManagedIdentityE2ETests.ManagedIdentity_E2E_CommandWithToken_ShouldUseTokenFromProfile [SKIP]
  Skipped Cli.Tests.ManagedIdentityE2ETests.ManagedIdentity_E2E_SystemAssigned_ShouldLoginAndSaveProfile [1 ms]
  Failed Cli.Tests.ManagedIdentityE2ETests.ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail [2 ms]
  Error Message:
   System.InvalidOperationException : CLI binary not found: C:\Users\jaysontai\Code\copilot-worktrees\cirrus\jaysontaimicrosoft-shiny-lamp\artifacts\Debug\Core.Cli\az
  Stack Trace:
     at Cli.Tests.ManagedIdentityE2ETests.ResolveCliBinary() in C:\Users\jaysontai\Code\copilot-worktrees\cirrus\jaysontaimicrosoft-shiny-lamp\tests\Cli.Tests\ManagedIdentityE2ETests.cs:line 343
   at Cli.Tests.ManagedIdentityE2ETests.ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail() in C:\Users\jaysontai\Code\copilot-worktrees\cirrus\jaysontaimicrosoft-shiny-lamp\tests\Cli.Tests\ManagedIdentityE2ETests.cs:line 296
--- End of stack trace from previous location ---
  Skipped Cli.Tests.ManagedIdentityE2ETests.ManagedIdentity_E2E_CommandWithToken_ShouldUseTokenFromProfile [1 ms]
[xUnit.net 00:00:07.19]     Cli.Tests.ManagedIdentityE2ETests.ManagedIdentity_E2E_UserAssignedClientId_ShouldLoginAndSaveProfile [SKIP]
  Skipped Cli.Tests.ManagedIdentityE2ETests.ManagedIdentity_E2E_UserAssignedClientId_ShouldLoginAndSaveProfile [1 ms]

Failed!  - Failed:     1, Passed:   597, Skipped:     7, Total:   605, Duration: 7 s - Cli.Tests.dll (net10.0)

Self-review:
- Implemented focused change in src\CLI\Program.cs only.
- Wrapped root activity creation and performance.Replay(_telemetryService) in a performance.Measure("CLI.Performance.Telemetry") block so both are captured under the same recorded phase at startup.
- Preserved all other local changes in Program.cs and did not modify DefaultTelemetryActivityNameFilter.cs or other files.
- Ran the Cli.Tests project; one unrelated E2E managed-identity test failed due to a missing CLI binary under artifacts (test environment issue), not due to this change.

Concerns:
- The failing test (ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail) expects the built CLI binary at artifacts\Debug\Core.Cli\az; CI or local environment should produce that binary before running E2E tests. The change made is small and limited to Program.cs and should not affect that behavior.
- Please run the full test suite in CI where build artifacts are staged to verify there are no regressions.

Exact diff (summary):
- Wrapped StartRootActivity(performance.StartedAt) and performance.Replay(_telemetryService) inside using (performance.Measure("CLI.Performance.Telemetry")) { ... }


