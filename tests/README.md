# Running tests

Use the .NET SDK pinned in the repository's `global.json`. Build the solution
before running binary-backed tests:

```sh
dotnet build azclips.slnx
dotnet test azclips.slnx --no-build --no-restore
```

For a focused run, use `dotnet test <test-project>.csproj`. To investigate timing,
add `--logger trx --results-directory <output-directory>` and repeat slow cases
with `--filter FullyQualifiedName~<test>` without unrelated build/test load.

## Build and packaging tests

The CLI extension-publish test publishes each extension from the matching
configuration's existing build output. It still inspects the actual published
files for forbidden host assemblies, but does not rebuild or restore each
extension inside the test. Run the solution build first, including when using
`--no-build` for the test project.

## Scenario and timing tests

Offline network fakes return `Retry-After: 0`: these scenarios verify request
bodies, operation polling, and output, not elapsed retry pacing. Recording against
a fake must be fast as well as playback. Real HTTP responses and production
polling behavior are unchanged.

Tests whose contract is an actual timeout retain their production deadlines.
The model-query budget test holds its fake response until the deadline assertion
can be evaluated, then releases it so a late reply does not prolong cleanup.
