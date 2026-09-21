#!/usr/bin/env bash
# Tests for generate_ps_validation_matrix.sh.

set -uo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
WORK_DIR="$(mktemp -d)" || exit 1
trap 'rm -rf "$WORK_DIR"' EXIT

failures=0
tests_run=0

# jq builds for Windows terminate lines with CRLF; strip CR so assertions compare cleanly.
jq() {
  command jq "$@" | tr -d '\r'
}

assert_equals() {
  local expected="$1" actual="$2" message="$3"
  if [ "$expected" = "$actual" ]; then
    return 0
  fi
  printf '  FAIL: %s\n    expected: %s\n    actual:   %s\n' \
    "$message" "$expected" "$actual" >&2
  failures=$((failures + 1))
}

assert_contains() {
  local haystack="$1" needle="$2" message="$3"
  case "$haystack" in
    *"$needle"*) return 0 ;;
  esac
  printf '  FAIL: %s\n    expected to contain: %s\n    actual: %s\n' \
    "$message" "$needle" "$haystack" >&2
  failures=$((failures + 1))
}

get_expected_az_module_version() {
  local manifest="${SCRIPT_DIR}/../../../../src/PS/Az/Az.psd1"
  sed -n "s/^[[:space:]]*ModuleVersion[[:space:]]*=[[:space:]]*'\([^']*\)'.*/\1/p" "$manifest" |
    head -n 1 |
    tr -d '\r'
}

start_test() {
  tests_run=$((tests_run + 1))
  printf '%s ... ' "$1"
}

end_test() {
  printf 'done\n'
}

write_config() {
  cat >"$WORK_DIR/config.json" <<'EOF'
{
  "schemaVersion": 3,
  "powershell": {
    "defaultVersions": [ "7.6" ],
    "versions": [
      { "version": "7.5", "dotnetRuntime": "9.0", "supported": false },
      { "version": "7.6", "dotnetRuntime": "10.0" }
    ],
    "versionCheck": {
      "enabled": true,
      "source": "github",
      "repository": "PowerShell/PowerShell",
      "tagPattern": "^v(7\\.[0-9]+)\\.[0-9]+$"
    }
  },
  "distributions": [
    {
      "id": "alpine",
      "title": "Alpine",
      "family": "alpine",
      "executionMode": "container",
      "pool": "ubuntu-latest",
      "imageTemplate": "alpine:{version}",
      "versions": [
        { "version": "3.23" },
        { "version": "3.24" }
      ],
      "versionCheck": {
        "enabled": true,
        "repository": "library/alpine",
        "tagPattern": "^([0-9]+\\.[0-9]+)$"
      }
    }
  ]
}
EOF
}

write_native_config() {
  cat >"$WORK_DIR/native.json" <<'EOF'
{
  "schemaVersion": 3,
  "powershell": {
    "defaultVersions": [ "7.6" ],
    "versions": [
      { "version": "7.5", "dotnetRuntime": "9.0", "supported": false },
      { "version": "7.6", "dotnetRuntime": "10.0" }
    ],
    "versionCheck": { "enabled": false }
  },
  "distributions": [
    {
      "id": "alpine",
      "title": "Alpine",
      "family": "alpine",
      "executionMode": "container",
      "pool": "ubuntu-latest",
      "imageTemplate": "alpine:{version}",
      "versions": [
        { "version": "3.23" }
      ],
      "versionCheck": { "enabled": false }
    },
    {
      "id": "macos_arm64",
      "title": "macOS",
      "titleSuffix": " (arm64)",
      "family": "macos",
      "executionMode": "native",
      "poolTemplate": "macOS-{version}-arm64",
      "versions": [
        { "version": "15" }
      ],
      "versionCheck": { "enabled": false }
    }
  ]
}
EOF
}

write_powershell_config() {
  cat >"$WORK_DIR/powershell.json" <<'EOF'
{
  "schemaVersion": 3,
  "powershell": {
    "defaultVersions": [ "7.6" ],
    "versions": [
      { "version": "7.4", "dotnetRuntime": "8.0", "supported": false },
      { "version": "7.5", "dotnetRuntime": "9.0", "supported": false },
      { "version": "7.6", "dotnetRuntime": "10.0" }
    ],
    "versionCheck": {
      "enabled": true,
      "source": "github",
      "repository": "PowerShell/PowerShell",
      "tagPattern": "^v(7\\.[0-9]+)\\.[0-9]+$"
    }
  },
  "distributions": [
    {
      "id": "alpine",
      "title": "Alpine",
      "family": "alpine",
      "executionMode": "container",
      "pool": "ubuntu-latest",
      "imageTemplate": "alpine:{version}",
      "versions": [
        { "version": "3.23" }
      ],
      "versionCheck": { "enabled": false }
    },
    {
      "id": "windows",
      "title": "Windows Server",
      "family": "windows",
      "executionMode": "native",
      "poolTemplate": "windows-{version}",
      "versions": [
        { "version": "2022" },
        { "version": "2025" }
      ],
      "powershellVersions": [ "7.4", "7.5", "7.6" ],
      "powershellVersionCheck": true,
      "versionCheck": { "enabled": false }
    }
  ]
}
EOF
}

# Runs the generator with get_docker_tags replaced by a fixture.
run_with_tags() {
  local tags="$1"
  shift
  AZCLIPS_MATRIX_LIB_ONLY=1 bash -c '
    source "$1"
    shift
    fixture_tags="$1"
    shift
    get_docker_tags() { printf "%s" "$fixture_tags"; }
    main "$@"
  ' _ "$SCRIPT_DIR/generate_ps_validation_matrix.sh" "$tags" "$@"
}

# Runs the generator with get_github_tags replaced by a fixture.
run_with_github_tags() {
  local tags="$1"
  shift
  AZCLIPS_MATRIX_LIB_ONLY=1 bash -c '
    source "$1"
    shift
    fixture_tags="$1"
    shift
    get_docker_tags() { return 1; }
    get_github_tags() { printf "%s" "$fixture_tags"; }
    main "$@"
  ' _ "$SCRIPT_DIR/generate_ps_validation_matrix.sh" "$tags" "$@"
}

run_with_failing_registry() {
  AZCLIPS_MATRIX_LIB_ONLY=1 bash -c '
    source "$1"
    shift
    get_docker_tags() { return 1; }
    get_github_tags() { return 1; }
    main "$@"
  ' _ "$SCRIPT_DIR/generate_ps_validation_matrix.sh" "$@"
}

# Inject a jq error only during generation, after the configuration has validated.
run_with_jq_failure() {
  local filter="$1"
  shift
  AZCLIPS_MATRIX_LIB_ONLY=1 bash -c '
    source "$1"
    shift
    failure_filter="$1"
    shift
    get_docker_tags() { return 0; }
    get_github_tags() { return 0; }
    jq_run() {
      local argument
      if [[ " ${FUNCNAME[*]} " == *" build_matrix "* ]]; then
        for argument in "$@"; do
          if [ "$argument" = "$failure_filter" ]; then
            printf "Injected jq failure\n" >&2
            return 73
          fi
        done
      fi
      jq "$@" | tr -d "\r"
    }
    main "$@"
  ' _ "$SCRIPT_DIR/generate_ps_validation_matrix.sh" "$filter" "$@"
}

assert_rejected_config() {
  local filter="$1" message="$2" fixture="${3:-$WORK_DIR/config.json}" output status
  jq "$filter" "$fixture" >"$WORK_DIR/invalid.json" || exit 1
  output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
    --config "$WORK_DIR/invalid.json" --skip-version-check --azure-variable matrix \
    2>"$WORK_DIR/invalid-stderr.txt")"
  status=$?
  assert_equals "1" "$status" "invalid configuration exits nonzero"
  assert_equals "" "$output" "invalid configuration emits no partial matrix or Azure variable"
  assert_contains "$(cat "$WORK_DIR/invalid-stderr.txt")" "$message" "configuration error"
}

write_config

start_test "adds latest new version as unsupported"
output="$(run_with_tags $'3.23\n3.24\n3.25\n3.26\nlatest' --config "$WORK_DIR/config.json")"
assert_equals "0" "$?" "exit code"
assert_equals "3" "$(jq -r 'length' <<<"$output")" "matrix leg count"
assert_equals "Alpine 3.26 on PowerShell 7.6 (unsupported)" \
  "$(jq -r '.alpine_3_26_ps7_6_unsupported.DistributionName' <<<"$output")" "unsupported title"
assert_equals "true" \
  "$(jq -r '.alpine_3_26_ps7_6_unsupported.ContinueOnError' <<<"$output")" "soft-fail flag"
assert_equals "true" \
  "$(jq -r '.alpine_3_26_ps7_6_unsupported.TestInAlpha' <<<"$output")" "alpha flag"
assert_equals "alpine:3.26" \
  "$(jq -r '.alpine_3_26_ps7_6_unsupported.ContainerImage' <<<"$output")" "image template"
end_test

start_test "ignores registry tags that are not newer than configured versions"
output="$(run_with_tags $'3.20\n3.23\n3.24\nedge' --config "$WORK_DIR/config.json")"
assert_equals "0" "$?" "exit code"
assert_equals "2" "$(jq -r 'length' <<<"$output")" "matrix leg count"
end_test

start_test "orders versions numerically rather than lexically"
output="$(run_with_tags $'3.9\n3.100\n3.25' --config "$WORK_DIR/config.json")"
assert_equals "0" "$?" "exit code"
assert_equals "Alpine 3.100 on PowerShell 7.6 (unsupported)" \
  "$(jq -r '.alpine_3_100_ps7_6_unsupported.DistributionName' <<<"$output")" "highest version wins"
end_test

start_test "matrix keys are valid azure job names"
output="$(run_with_tags $'3.25' --config "$WORK_DIR/config.json")"
assert_equals "0" "$?" "exit code"
keys="$(jq -r 'keys_unsorted[]' <<<"$output")"
assert_equals "0" "$?" "matrix key extraction"
while IFS= read -r key; do
  if [[ ! "$key" =~ ^[A-Za-z][A-Za-z0-9_]{0,99}$ ]]; then
    printf '  FAIL: invalid matrix key %s\n' "$key" >&2
    failures=$((failures + 1))
  fi
done <<<"$keys"
assert_equals "true" "$(jq 'all(to_entries[]; .key == .value.ValidationKey)' <<<"$output")" \
  "every artifact key matches its unique matrix leg"
end_test

start_test "registry failure falls back to configured versions"
output="$(run_with_failing_registry --config "$WORK_DIR/config.json" 2>"$WORK_DIR/stderr.txt")"
assert_equals "0" "$?" "exit code"
assert_equals "alpine_3_23_ps7_6 alpine_3_24_ps7_6" \
  "$(jq -r 'keys_unsorted | join(" ")' <<<"$output")" "configured legs retained"
assert_contains "$(cat "$WORK_DIR/stderr.txt")" "Unable to check for new 'alpine' versions" \
  "fallback warning"
end_test

start_test "registry failure warnings do not corrupt the azure output variable"
output="$(run_with_failing_registry --config "$WORK_DIR/config.json" --azure-variable matrix 2>&1)"
assert_equals "0" "$?" "exit code"
assert_contains "$output" "##vso[task.logissue type=warning]Unable to check for new 'alpine' versions" \
  "warning is reported as a logging command"
assert_equals "alpine_3_23_ps7_6 alpine_3_24_ps7_6" \
  "$(grep -o '{.*}' <<<"$output" | jq -r 'keys_unsorted | join(" ")')" "matrix json is well formed"
end_test

start_test "skips registry check when requested"
output="$(AZCLIPS_MATRIX_LIB_ONLY=1 bash -c '
  source "$1"
  shift
  get_docker_tags() { echo "get_docker_tags should not be called" >&2; return 99; }
  main "$@"
' _ "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --config "$WORK_DIR/config.json" --skip-version-check)"
assert_equals "0" "$?" "exit code"
assert_equals "2" "$(jq -r 'length' <<<"$output")" "matrix leg count"
assert_equals "false false" \
  "$(jq -r '[.[].ContinueOnError] | join(" ")' <<<"$output")" "no soft-fail legs"
end_test

start_test "emits the powershell version and its dotnet runtime"
output="$(run_with_tags "" --config "$WORK_DIR/config.json" --skip-version-check)"
assert_equals "0" "$?" "exit code"
assert_equals "7.6" \
  "$(jq -r '.alpine_3_23_ps7_6.PowerShellVersion' <<<"$output")" "powershell version"
assert_equals "10.0" \
  "$(jq -r '.alpine_3_23_ps7_6.DotnetRuntimeVersion' <<<"$output")" "dotnet runtime"
assert_equals "alpine" \
  "$(jq -r '.alpine_3_23_ps7_6.DistributionId' <<<"$output")" "distribution id"
assert_equals "devops/scripts/packaging/powershell/linux/alpine_install_script.sh" \
  "$(jq -r '.alpine_3_23_ps7_6.DistributionScript' <<<"$output")" "distribution script"
assert_equals "Alpine 3.23 on PowerShell 7.6" \
  "$(jq -r '.alpine_3_23_ps7_6.DistributionName' <<<"$output")" "leg title"
end_test

start_test "emits the az version on every matrix leg"
output="$(run_with_tags "" --config "$WORK_DIR/config.json" --skip-version-check)"
assert_equals "0" "$?" "exit code"
expected_az_version="$(get_expected_az_module_version)"
assert_equals "$expected_az_version" \
  "$(jq -r '.alpine_3_23_ps7_6.AzVersion' <<<"$output")" "az version"
assert_equals "0" \
  "$(jq -r '[.[] | select((has("AzVersion") | not) or (.AzVersion == ""))] | length' <<<"$output")" \
  "every leg has az version"
end_test

start_test "overrides distribution pool"
output="$(run_with_tags "" --config "$WORK_DIR/config.json" --skip-version-check --pool custom-linux-pool)"
assert_equals "0" "$?" "exit code"
assert_equals "custom-linux-pool custom-linux-pool" \
  "$(jq -r '[.[].DistributionPool] | join(" ")' <<<"$output")" "pool override"
end_test

start_test "emits an azure output variable"
output="$(run_with_tags "" --config "$WORK_DIR/config.json" --skip-version-check --azure-variable matrix)"
assert_equals "0" "$?" "exit code"
assert_contains "$output" "##vso[task.setvariable variable=matrix;isOutput=true]{" "azure logging command"
end_test

start_test "rejects distribution id that is not a valid job name"
jq '.distributions[0].id = "1-alpine"' "$WORK_DIR/config.json" >"$WORK_DIR/bad-id.json"
error_output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --config "$WORK_DIR/bad-id.json" 2>&1)"
assert_equals "1" "$?" "exit code"
assert_contains "$error_output" "must start with a letter" "distribution id error"
end_test

start_test "rejects tag pattern without a capture group"
jq '.distributions[0].versionCheck.tagPattern = "^[0-9]+\\.[0-9]+$"' \
  "$WORK_DIR/config.json" >"$WORK_DIR/bad-pattern.json"
error_output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --config "$WORK_DIR/bad-pattern.json" 2>&1)"
assert_equals "1" "$?" "exit code"
assert_contains "$error_output" "must contain a capture group" "tag pattern error"
end_test

start_test "rejects duplicate matrix keys"
jq '.distributions[0].versions += [{ "version": "3.23" }]' \
  "$WORK_DIR/config.json" >"$WORK_DIR/duplicate.json"
error_output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --config "$WORK_DIR/duplicate.json" 2>&1)"
assert_equals "1" "$?" "exit code"
assert_contains "$error_output" "Duplicate matrix key 'alpine_3_23_ps7_6'" "duplicate key error"
end_test

start_test "rejects unsupported schema versions"
jq '.schemaVersion = 2' "$WORK_DIR/config.json" >"$WORK_DIR/schema.json"
error_output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --config "$WORK_DIR/schema.json" 2>&1)"
assert_equals "1" "$?" "exit code"
assert_contains "$error_output" "Only schemaVersion 3 is supported" "schema version error"
end_test

start_test "rejects powershell versions without a dotnet runtime"
jq 'del(.powershell.versions[] | select(.version == "7.6") | .dotnetRuntime)' \
  "$WORK_DIR/config.json" >"$WORK_DIR/no-runtime.json"
error_output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --config "$WORK_DIR/no-runtime.json" 2>&1)"
assert_equals "1" "$?" "exit code"
assert_contains "$error_output" "must declare the .NET runtime" "dotnet runtime error"
end_test

start_test "rejects undeclared powershell versions"
jq '.distributions[0].powershellVersions = [ "7.9" ]' \
  "$WORK_DIR/config.json" >"$WORK_DIR/undeclared-ps.json"
error_output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --config "$WORK_DIR/undeclared-ps.json" 2>&1)"
assert_equals "1" "$?" "exit code"
assert_contains "$error_output" "is not declared in the powershell.versions list" "undeclared version error"
end_test

start_test "rejects default powershell versions that are not declared"
jq '.powershell.defaultVersions = [ "7.9" ]' \
  "$WORK_DIR/config.json" >"$WORK_DIR/bad-default.json"
error_output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --config "$WORK_DIR/bad-default.json" 2>&1)"
assert_equals "1" "$?" "exit code"
assert_contains "$error_output" "Default PowerShell version '7.9'" "default version error"
end_test

write_native_config

start_test "native distributions run on a platform pool without a container image"
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/native.json" --skip-version-check)"
assert_equals "0" "$?" "exit code"
assert_equals "macOS 15 (arm64) on PowerShell 7.6" \
  "$(jq -r '.macos_arm64_15_ps7_6.DistributionName' <<<"$output")" "native title"
assert_equals "native" \
  "$(jq -r '.macos_arm64_15_ps7_6.ValidationMode' <<<"$output")" "native mode"
assert_equals "macOS-15-arm64" \
  "$(jq -r '.macos_arm64_15_ps7_6.DistributionPool' <<<"$output")" "pool template resolved"
assert_equals "" \
  "$(jq -r '.macos_arm64_15_ps7_6.ContainerImage' <<<"$output")" "no container image"
assert_equals "macos_arm64" \
  "$(jq -r '.macos_arm64_15_ps7_6.DistributionId' <<<"$output")" "macos distribution id"
assert_equals "devops/scripts/packaging/powershell/macos/macos_install_script.sh" \
  "$(jq -r '.macos_arm64_15_ps7_6.DistributionScript' <<<"$output")" "macos distribution script"
assert_equals "container" \
  "$(jq -r '.alpine_3_23_ps7_6.ValidationMode' <<<"$output")" "container mode"
end_test

start_test "pool override applies to container distributions only"
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/native.json" --skip-version-check --pool custom-linux-pool)"
assert_equals "0" "$?" "exit code"
assert_equals "custom-linux-pool" \
  "$(jq -r '.alpine_3_23_ps7_6.DistributionPool' <<<"$output")" "container pool overridden"
assert_equals "macOS-15-arm64" \
  "$(jq -r '.macos_arm64_15_ps7_6.DistributionPool' <<<"$output")" "native pool preserved"
end_test

start_test "pool-for overrides a single distribution"
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/native.json" --skip-version-check --pool-for macos_arm64=macOS-26-arm64)"
assert_equals "0" "$?" "exit code"
assert_equals "macOS-26-arm64" \
  "$(jq -r '.macos_arm64_15_ps7_6.DistributionPool' <<<"$output")" "targeted pool override"
assert_equals "ubuntu-latest" \
  "$(jq -r '.alpine_3_23_ps7_6.DistributionPool' <<<"$output")" "other pools untouched"
end_test

start_test "named container pools are not hosted image labels"
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/native.json" --skip-version-check --pool pool-ubuntu-2204)"
assert_equals "0" "$?" "exit code"
assert_equals "pool-ubuntu-2204" \
  "$(jq -r '.alpine_3_23_ps7_6.DistributionPoolName' <<<"$output")" "named Linux pool"
assert_equals "" \
  "$(jq -r '.alpine_3_23_ps7_6.DistributionVmImage' <<<"$output")" "named pool has no image label"
assert_equals "pool-ubuntu-2204" \
  "$(jq -r '.alpine_3_23_ps7_6.DistributionPool' <<<"$output")" "diagnostic pool preserved"
assert_equals "Azure Pipelines" \
  "$(jq -r '.macos_arm64_15_ps7_6.DistributionPoolName' <<<"$output")" "macOS remains hosted"
assert_equals "macOS-15-arm64" \
  "$(jq -r '.macos_arm64_15_ps7_6.DistributionVmImage' <<<"$output")" "macOS image preserved"
end_test

start_test "routing applies after per-distribution overrides"
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/native.json" --skip-version-check --pool pool-ubuntu-2204 \
  --pool-for alpine=ubuntu-24.04 --pool-for macos_arm64=pool-macos-arm64)"
assert_equals "0" "$?" "exit code"
assert_equals "Azure Pipelines|ubuntu-24.04" \
  "$(jq -r '.alpine_3_23_ps7_6 | [.DistributionPoolName, .DistributionVmImage] | join("|")' <<<"$output")" \
  "explicit hosted override wins over named default"
assert_equals "pool-macos-arm64|" \
  "$(jq -r '.macos_arm64_15_ps7_6 | [.DistributionPoolName, .DistributionVmImage] | join("|")' <<<"$output")" \
  "explicit native named pool"
end_test

start_test "rejects version checking on native distributions"
jq '(.distributions[] | select(.id == "macos_arm64") | .versionCheck) =
  { "enabled": true, "repository": "library/macos", "tagPattern": "^([0-9]+)$" }' \
  "$WORK_DIR/native.json" >"$WORK_DIR/native-check.json"
error_output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --config "$WORK_DIR/native-check.json" 2>&1)"
assert_equals "1" "$?" "exit code"
assert_contains "$error_output" "cannot enable version checking" "native version check error"
end_test

start_test "rejects native distributions without a pool template"
jq 'del(.distributions[] | select(.id == "macos_arm64") | .poolTemplate)' \
  "$WORK_DIR/native.json" >"$WORK_DIR/native-nopool.json"
error_output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --config "$WORK_DIR/native-nopool.json" 2>&1)"
assert_equals "1" "$?" "exit code"
assert_contains "$error_output" "missing required property 'poolTemplate'" "native pool template error"
end_test

start_test "rejects unknown execution modes"
jq '(.distributions[] | select(.id == "macos_arm64") | .executionMode) = "vm"' \
  "$WORK_DIR/native.json" >"$WORK_DIR/native-mode.json"
error_output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --config "$WORK_DIR/native-mode.json" 2>&1)"
assert_equals "1" "$?" "exit code"
assert_contains "$error_output" "unsupported executionMode 'vm'" "execution mode error"
end_test

write_powershell_config

start_test "fans a distribution out across its powershell versions"
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/powershell.json" --skip-version-check)"
assert_equals "0" "$?" "exit code"
assert_equals "7" "$(jq -r 'length' <<<"$output")" "matrix leg count"
assert_equals "windows_2022_ps7_4_unsupported windows_2022_ps7_5_unsupported windows_2022_ps7_6" \
  "$(jq -r '[keys_unsorted[] | select(startswith("windows_2022"))] | join(" ")' <<<"$output")" \
  "windows 2022 legs"
assert_equals "Windows Server 2025 on PowerShell 7.6" \
  "$(jq -r '.windows_2025_ps7_6.DistributionName' <<<"$output")" "supported windows title"
assert_equals "windows-2025" \
  "$(jq -r '.windows_2025_ps7_6.DistributionPool' <<<"$output")" "windows pool template"
assert_equals "Azure Pipelines|windows-2025" \
  "$(jq -r '.windows_2025_ps7_6 | [.DistributionPoolName, .DistributionVmImage] | join("|")' <<<"$output")" \
  "Windows hosted routing is preserved"
assert_equals "windows" \
  "$(jq -r '.windows_2025_ps7_6.DistributionId' <<<"$output")" "windows distribution id"
assert_equals "devops/scripts/packaging/powershell/windows/windows_install_script.ps1" \
  "$(jq -r '.windows_2025_ps7_6.DistributionScript' <<<"$output")" "windows distribution script"
assert_equals "7.6" \
  "$(jq -r '.alpine_3_23_ps7_6.PowerShellVersion' <<<"$output")" "default versions used when unspecified"
end_test

start_test "accepts the latest native Windows hosted-agent alias alongside numeric versions"
jq '.distributions[1].versions += [{"version": "latest"}]' "$WORK_DIR/powershell.json" \
  >"$WORK_DIR/windows-latest.json" || exit 1
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/windows-latest.json" --skip-version-check --pool custom-linux)"
assert_equals "0" "$?" "exit code"
assert_equals "10" "$(jq -r 'length' <<<"$output")" "numeric and latest Windows legs"
assert_equals "windows_latest_ps7_4_unsupported windows_latest_ps7_5_unsupported windows_latest_ps7_6" \
  "$(jq -r '[keys_unsorted[] | select(startswith("windows_latest"))] | join(" ")' <<<"$output")" \
  "latest Windows PowerShell versions"
assert_equals "windows-2022 windows-2025 windows-latest" \
  "$(jq -r '[.windows_2022_ps7_6.DistributionPool, .windows_2025_ps7_6.DistributionPool, .windows_latest_ps7_6.DistributionPool] | join(" ")' <<<"$output")" \
  "native pools ignore the container pool override"
assert_equals "native" "$(jq -r '.windows_latest_ps7_6.ValidationMode' <<<"$output")" "native execution"
assert_equals "" "$(jq -r '.windows_latest_ps7_6.ContainerImage' <<<"$output")" "no container image"
assert_equals "devops/scripts/packaging/powershell/windows/windows_install_script.ps1" \
  "$(jq -r '.windows_latest_ps7_6.DistributionScript' <<<"$output")" "Windows bootstrap"
assert_equals "10.0 false false" \
  "$(jq -r '[.windows_latest_ps7_6 | .DotnetRuntimeVersion, .ContinueOnError, .TestInAlpha] | join(" ")' <<<"$output")" \
  "configured latest is blocking and is not an automatically selected canary"
end_test

start_test "allows explicit pool overrides for the latest Windows alias"
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/windows-latest.json" --skip-version-check --pool-for windows=custom-windows)"
assert_equals "0" "$?" "exit code"
assert_equals "custom-windows" \
  "$(jq -r '.windows_latest_ps7_6.DistributionPool' <<<"$output")" "explicit native pool override"
end_test

start_test "discovers PowerShell canaries on the latest Windows alias"
output="$(run_with_github_tags $'v7.6.0\nv7.7.1' --config "$WORK_DIR/windows-latest.json")"
assert_equals "0" "$?" "exit code"
assert_equals "13" "$(jq -r 'length' <<<"$output")" "numeric and alias PowerShell canaries"
assert_equals "windows-latest 11.0 true true" \
  "$(jq -r '[.windows_latest_ps7_7_unsupported | .DistributionPool, .DotnetRuntimeVersion, .ContinueOnError, .TestInAlpha] | join(" ")' <<<"$output")" \
  "discovered PowerShell remains unsupported on the latest Windows pool"
end_test

start_test "limited mode selects the latest Windows alias only when explicitly opted in"
jq '.distributions[1].versions[-1].testInAlpha = true' "$WORK_DIR/windows-latest.json" \
  >"$WORK_DIR/windows-latest-alpha.json" || exit 1
output="$(run_with_github_tags $'v7.6.0\nv7.7.1' --config "$WORK_DIR/windows-latest-alpha.json" --limit-testing)"
assert_equals "0" "$?" "exit code"
assert_equals "windows_latest_ps7_4_unsupported windows_latest_ps7_5_unsupported windows_latest_ps7_6" \
  "$(jq -r 'keys_unsorted | join(" ")' <<<"$output")" "only configured alias legs, without discovery"
assert_equals '["true","true","false"]' "$(jq -c '[.[].ContinueOnError]' <<<"$output")" \
  "configured PowerShell support policy is preserved"
end_test

start_test "powershell versions the modules cannot load are soft-fail legs"
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/powershell.json" --skip-version-check)"
assert_equals "0" "$?" "exit code"
assert_equals "Windows Server 2022 on PowerShell 7.4 (unsupported)" \
  "$(jq -r '.windows_2022_ps7_4_unsupported.DistributionName' <<<"$output")" "unsupported title"
assert_equals "true" \
  "$(jq -r '.windows_2022_ps7_4_unsupported.ContinueOnError' <<<"$output")" "soft-fail flag"
assert_equals "8.0" \
  "$(jq -r '.windows_2022_ps7_4_unsupported.DotnetRuntimeVersion' <<<"$output")" "matching dotnet runtime"
assert_equals "false" \
  "$(jq -r '.windows_2022_ps7_6.ContinueOnError' <<<"$output")" "supported leg is not soft-fail"
end_test

start_test "adds a newly released powershell version as unsupported"
output="$(run_with_github_tags $'v7.4.6\nv7.6.0\nv7.7.0\nv7.7.1' \
  --config "$WORK_DIR/powershell.json")"
assert_equals "0" "$?" "exit code"
assert_equals "9" "$(jq -r 'length' <<<"$output")" "matrix leg count"
assert_equals "Windows Server 2022 on PowerShell 7.7 (unsupported)" \
  "$(jq -r '.windows_2022_ps7_7_unsupported.DistributionName' <<<"$output")" "canary title"
assert_equals "11.0" \
  "$(jq -r '.windows_2022_ps7_7_unsupported.DotnetRuntimeVersion' <<<"$output")" "derived dotnet runtime"
assert_equals "true true" \
  "$(jq -r '[.windows_2022_ps7_7_unsupported | .TestInAlpha, .ContinueOnError] | join(" ")' <<<"$output")" \
  "PowerShell canary is included in alpha and soft-fails"
assert_equals "null" \
  "$(jq -r '.alpine_3_23_ps7_7_unsupported.DistributionName' <<<"$output")" \
  "distributions without powershellVersionCheck are untouched"
end_test

start_test "powershell registry failure falls back to configured versions"
output="$(run_with_failing_registry --config "$WORK_DIR/powershell.json" 2>"$WORK_DIR/ps-stderr.txt")"
assert_equals "0" "$?" "exit code"
assert_equals "7" "$(jq -r 'length' <<<"$output")" "matrix leg count"
assert_contains "$(cat "$WORK_DIR/ps-stderr.txt")" "Unable to check for new PowerShell versions" \
  "fallback warning"
end_test

start_test "repository configuration is valid"
python3 "$SCRIPT_DIR/read_ps_validation_yaml.py" "$SCRIPT_DIR/ps_validation_matrix.yaml" \
  >"$WORK_DIR/repository.json" || exit 1
expected_count="$(jq -r '
  [.distributions[]
   | (.versions | length) * ((.powershellVersions // $ARGS.named.defaults) | length)]
  | add' --argjson defaults "$(jq -c '.powershell.defaultVersions' "$WORK_DIR/repository.json")" \
  "$WORK_DIR/repository.json")"
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$SCRIPT_DIR/ps_validation_matrix.yaml" --skip-version-check)"
assert_equals "0" "$?" "exit code"
assert_equals "$expected_count" "$(jq -r 'length' <<<"$output")" "configured leg count"
assert_equals "container,native" \
  "$(jq -r '[.[].ValidationMode] | unique | join(",")' <<<"$output")" "both execution modes covered"
assert_equals "macOS-15-arm64 macOS-15" \
  "$(jq -r '[.macos_arm64_15_ps7_6.DistributionPool, .macos_x64_15_ps7_6.DistributionPool] | join(" ")' <<<"$output")" \
  "macos pools"
assert_equals "windows-2022 windows-2025" \
  "$(jq -r '[.[] | select(.DistributionId == "windows") | .DistributionPool] | unique | join(" ")' <<<"$output")" \
  "windows pools"
assert_equals "ubuntu" \
  "$(jq -r '.ubuntu_24_04_ps7_6.DistributionId' <<<"$output")" "ubuntu distribution id"
assert_equals "devops/scripts/packaging/powershell/linux/ubuntu_install_script.sh" \
  "$(jq -r '.ubuntu_24_04_ps7_6.DistributionScript' <<<"$output")" "ubuntu distribution script"
assert_equals "false" \
  "$(jq -r '[.[] | select(.PowerShellVersion == "7.6") | .ContinueOnError] | unique | join(",")' <<<"$output")" \
  "supported powershell legs are not soft-fail"
end_test

for value in '{}' null '[]' '"3.23"'; do
  start_test "rejects distribution versions of malformed type $value"
  assert_rejected_config ".distributions[0].versions = $value" "versions must be a nonempty array"
  end_test
done

start_test "rejects malformed later distributions rather than emitting the earlier legs"
assert_rejected_config '.distributions[1].versions = {"version": "15"}' \
  "versions must be a nonempty array" "$WORK_DIR/native.json"
end_test

for value in '[]' null '"powershell"'; do
  start_test "rejects malformed powershell object $value"
  assert_rejected_config ".powershell = $value" "powershell must be an object"
  end_test
done

for field in versions defaultVersions; do
  for value in '{}' null '[]' '"7.6"'; do
    start_test "rejects malformed powershell.$field $value"
    assert_rejected_config ".powershell.$field = $value" "powershell.$field must be a nonempty array"
    end_test
  done
done

for value in '{}' null '[]' '"7.6"' '[7.6]'; do
  start_test "rejects malformed distribution powershellVersions $value"
  assert_rejected_config ".distributions[0].powershellVersions = $value" "powershellVersions must"
  end_test
done

for value in '{}' null '[]' '[null]'; do
  start_test "rejects malformed distributions $value"
  assert_rejected_config ".distributions = $value" \
    "$([ "$value" = '[null]' ] && printf 'Distribution entries must be objects' || printf 'distributions must be a nonempty array')"
  end_test
done

for filter in \
  '.distributions[0].versions[0] = "3.23"' \
  '.powershell.versions[0] = "7.6"'; do
  start_test "rejects scalar version entries: $filter"
  assert_rejected_config "$filter" "version entries must be objects"
  end_test
done

for filter in \
  '.distributions[0].versions[0].version = 3.23' \
  '.distributions[0].versions[0].version = "latest"' \
  '.distributions[0].powershellVersions = ["latest"]' \
  '.powershell.versions[0].version = 7.5' \
  '.powershell.versions[0].version = "latest"' \
  '.powershell.defaultVersions = [7.6]' \
  '.powershell.defaultVersions = ["latest"]' \
  '.powershell.versions[0].dotnetRuntime = 9.0' \
  '.powershell.versions[0].dotnetRuntime = "latest"' \
  '.powershell.versions[0].dotnetRuntime = "net9.0"'; do
  start_test "rejects nonnumeric or nonstring versions: $filter"
  assert_rejected_config "$filter" "numeric version string"
  end_test
done

for value in '"windows-latest"' '"Latest"' '"preview"' 2025 null; do
  start_test "rejects invalid native Windows version $value"
  assert_rejected_config ".distributions[1].versions[0].version = $value" \
    "numeric version strings" "$WORK_DIR/powershell.json"
  end_test
done

start_test "does not extend the latest alias to native macOS versions"
assert_rejected_config '.distributions[1].versions[0].version = "latest"' \
  "numeric version strings" "$WORK_DIR/native.json"
end_test

start_test "does not extend the latest alias to container Windows versions"
assert_rejected_config '.distributions[1].executionMode = "container"' \
  "numeric version strings" "$WORK_DIR/windows-latest.json"
end_test

start_test "latest Windows still rejects container-registry version discovery"
assert_rejected_config '.distributions[1].versionCheck = {
  "enabled": true, "repository": "library/windows", "tagPattern": "^([0-9]+)$"
}' "cannot enable version checking" "$WORK_DIR/windows-latest.json"
end_test

for field in \
  '.distributions[0].versions[0].testInAlpha' \
  '.distributions[0].powershellVersionCheck' \
  '.distributions[0].versionCheck.enabled' \
  '.powershell.versionCheck.enabled' \
  '.powershell.versions[0].supported'; do
  for value in '"false"' null 1; do
    start_test "rejects nonboolean $field = $value"
    assert_rejected_config "$field = $value" "must be a boolean"
    end_test
  done
done

for filter in \
  '.distributions[0].title = ""' \
  '.distributions[0].pool = []' \
  '.distributions[0].imageTemplate = null' \
  '.distributions[0].versionCheck.repository = 123' \
  '.powershell.versionCheck.tagPattern = []'; do
  start_test "rejects malformed required strings: $filter"
  assert_rejected_config "$filter" "must be a nonempty string"
  end_test
done

for field in '.distributions[0].versionCheck' '.powershell.versionCheck'; do
  start_test "rejects array $field"
  assert_rejected_config "$field = []" "versionCheck must be an object"
  end_test
done

start_test "rejects duplicate distribution ids even with disjoint versions"
assert_rejected_config '.distributions += [(.distributions[0] | .versions = [{"version": "3.25"}])]' \
  "Duplicate distribution id 'alpine'"
end_test

start_test "rejects duplicate PowerShell runtime declarations"
assert_rejected_config '.powershell.versions += [.powershell.versions[0]]' \
  "Duplicate PowerShell version '7.5'"
end_test

start_test "rejects unsupported PowerShell discovery sources"
assert_rejected_config '.powershell.versionCheck.source = "other"' "unsupported source"
end_test

start_test "rejects distribution discovery sources that are not Docker Hub"
assert_rejected_config '.distributions[0].versionCheck.source = "github"' "unsupported source"
end_test

start_test "rejects unsupported distribution families"
assert_rejected_config '.distributions[0].family = "unknown"' "unsupported family/executionMode"
end_test

start_test "rejects container execution for a native platform"
assert_rejected_config '.distributions[0].family = "windows"' "unsupported family/executionMode"
end_test

start_test "rejects native execution for a Linux distribution"
assert_rejected_config '.distributions[0].executionMode = "native"' "unsupported family/executionMode"
end_test

start_test "rejects missing distribution bootstrap scripts"
assert_rejected_config '.distributions[0].id = "missing_test_distribution"' \
  "DistributionScript 'devops/scripts/packaging/powershell/linux/missing_test_distribution_install_script.sh'"
end_test

start_test "rejects invalid tag regular expressions"
assert_rejected_config '.distributions[0].versionCheck.tagPattern = "([0-9]+"' \
  "not a valid regular expression"
end_test

for json in '{' '[]' 'null' '{} {}'; do
  start_test "rejects malformed or multiple JSON documents: $json"
  printf '%s\n' "$json" >"$WORK_DIR/invalid-json.json"
  output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
    --config "$WORK_DIR/invalid-json.json" --azure-variable matrix 2>"$WORK_DIR/invalid-stderr.txt")"
  assert_equals "1" "$?" "exit code"
  assert_equals "" "$output" "no success-shaped output"
  assert_contains "$(cat "$WORK_DIR/invalid-stderr.txt")" "Invalid matrix configuration" "invalid JSON error"
  end_test
done

for filter in \
  '.distributions[1]' \
  '.powershellVersions[]' \
  '.versions[] | [.version, (.testInAlpha // false | tostring)] | @tsv' \
  '.title' \
  '.pool' \
  '.powershell.versionCheck.enabled // false'; do
  start_test "propagates generation jq failure for $filter"
  output="$(run_with_jq_failure "$filter" \
    --config "$WORK_DIR/powershell.json" --azure-variable matrix 2>"$WORK_DIR/failure-stderr.txt")"
  assert_equals "1" "$?" "exit code"
  assert_equals "" "$output" "no partial matrix or output variable"
  assert_contains "$(cat "$WORK_DIR/failure-stderr.txt")" "Injected jq failure" "jq error is not swallowed"
  end_test
done

for function in build_matrix emit_entry discover_powershell_version get_powershell_support get_az_module_version; do
  start_test "propagates a failing $function command substitution"
  output="$(AZCLIPS_MATRIX_LIB_ONLY=1 bash -c '
    source "$1"
    eval "$2() { printf \"{}\"; return 73; }"
    shift 2
    main "$@"
  ' _ "$SCRIPT_DIR/generate_ps_validation_matrix.sh" "$function" \
    --config "$WORK_DIR/config.json" --skip-version-check --azure-variable matrix \
    2>"$WORK_DIR/failure-stderr.txt")"
  assert_equals "1" "$?" "exit code"
  assert_equals "" "$output" "failed subprocess stdout cannot become a partial output variable"
  assert_contains "$(cat "$WORK_DIR/failure-stderr.txt")" "ERROR:" "generation failure"
  end_test
done

start_test "rejects an empty matrix even when an entry emitter returns success"
output="$(AZCLIPS_MATRIX_LIB_ONLY=1 bash -c '
  source "$1"
  shift
  emit_entry() { return 0; }
  main "$@"
' _ "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/config.json" --skip-version-check --azure-variable matrix \
  2>"$WORK_DIR/failure-stderr.txt")"
assert_equals "1" "$?" "exit code"
assert_equals "" "$output" "no empty matrix output"
assert_contains "$(cat "$WORK_DIR/failure-stderr.txt")" "Matrix generation must produce nonempty entries" \
  "empty generation error"
end_test

start_test "propagates version sorting errors rather than dropping discovery candidates"
jq '.distributions[0].versions = [.distributions[0].versions[0]]' "$WORK_DIR/config.json" \
  >"$WORK_DIR/sort.json" || exit 1
output="$(AZCLIPS_MATRIX_LIB_ONLY=1 bash -c '
  source "$1"
  shift
  sort() { return 73; }
  get_docker_tags() { printf "3.25\n"; }
  main "$@"
' _ "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/sort.json" --azure-variable matrix 2>"$WORK_DIR/failure-stderr.txt")"
assert_equals "1" "$?" "exit code"
assert_equals "" "$output" "no partial output after sort fails"
assert_contains "$(cat "$WORK_DIR/failure-stderr.txt")" "Unable to generate the validation matrix" "sort error"
end_test

start_test "failed optional discovery discards partial HTTP response data"
output="$(AZCLIPS_MATRIX_LIB_ONLY=1 bash -c '
  source "$1"
  shift
  get_docker_tags() { printf "3.99\n"; return 73; }
  main "$@"
' _ "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/config.json" 2>"$WORK_DIR/failure-stderr.txt")"
assert_equals "0" "$?" "exit code"
assert_equals "alpine_3_23_ps7_6 alpine_3_24_ps7_6" \
  "$(jq -r 'keys_unsorted | join(" ")' <<<"$output")" "partial response is not used"
assert_contains "$(cat "$WORK_DIR/failure-stderr.txt")" "Unable to check" "optional discovery warning"
end_test

start_test "full mode retains explicit true and false alpha flags"
jq '.distributions[0].versions[0].testInAlpha = false |
    .distributions[0].versions[1].testInAlpha = true' \
  "$WORK_DIR/config.json" >"$WORK_DIR/alpha.json" || exit 1
output="$(run_with_tags "" --config "$WORK_DIR/alpha.json" --skip-version-check)"
assert_equals "0" "$?" "exit code"
assert_equals "2" "$(jq -r 'length' <<<"$output")" "default mode includes every configured version"
assert_equals '["false","true"]' "$(jq -c '[.[].TestInAlpha]' <<<"$output")" "string alpha values"
end_test

start_test "limit-testing selects only configured true alpha flags"
output="$(run_with_tags "" --config "$WORK_DIR/alpha.json" --limit-testing)"
assert_equals "0" "$?" "exit code"
assert_equals "alpine_3_24_ps7_6" "$(jq -r 'keys | join(" ")' <<<"$output")" "only alpha leg selected"
assert_equals "false" "$(jq -r '.alpine_3_24_ps7_6.ContinueOnError' <<<"$output")" "configured alpha stays hard-fail"
end_test

start_test "limit-testing filters before Azure variable expansion and composes with pool overrides"
output="$(run_with_tags "" --config "$WORK_DIR/alpha.json" --limit-testing --skip-version-check \
  --pool custom-linux --azure-variable matrix)"
assert_equals "0" "$?" "exit code"
assert_contains "$output" "##vso[task.setvariable variable=matrix;isOutput=true]{" "Azure variable"
matrix="${output#*]}"
assert_equals "alpine_3_24_ps7_6" "$(jq -r 'keys | join(" ")' <<<"$matrix")" "limited Azure matrix"
assert_equals "custom-linux" "$(jq -r '.[].DistributionPool' <<<"$matrix")" "pool option composed"
end_test

start_test "limited mode excludes unconfigured distro versions"
output="$(run_with_tags $'3.24\n3.25' --config "$WORK_DIR/alpha.json" --limit-testing)"
assert_equals "0" "$?" "exit code"
assert_equals "alpine_3_24_ps7_6" \
  "$(jq -r 'keys_unsorted | join(" ")' <<<"$output")" "configured alpha only"
end_test

start_test "limited mode cannot replace an empty configured selection with a canary"
output="$(run_with_tags "3.25" --config "$WORK_DIR/config.json" --limit-testing \
  --azure-variable matrix 2>"$WORK_DIR/failure-stderr.txt")"
assert_equals "1" "$?" "exit code"
assert_equals "" "$output" "no discovered-only Azure matrix"
assert_contains "$(cat "$WORK_DIR/failure-stderr.txt")" "--limit-testing selected no matrix legs" \
  "empty configured selection error"
end_test

start_test "limited mode retains only configured PowerShell versions on selected native versions"
jq '.distributions[1].versions[1].testInAlpha = true' "$WORK_DIR/powershell.json" \
  >"$WORK_DIR/native-alpha.json" || exit 1
output="$(run_with_github_tags $'v7.6.0\nv7.7.1' --config "$WORK_DIR/native-alpha.json" --limit-testing)"
assert_equals "0" "$?" "exit code"
assert_equals "windows_2025_ps7_4_unsupported windows_2025_ps7_5_unsupported windows_2025_ps7_6" \
  "$(jq -r 'keys_unsorted | join(" ")' <<<"$output")" "configured versions without a PowerShell canary"
assert_equals '["true","true","false"]' "$(jq -c '[.[].ContinueOnError]' <<<"$output")" \
  "explicitly configured unsupported versions retain their support policy"
end_test

jq '.distributions[0].powershellVersionCheck = true' "$WORK_DIR/alpha.json" \
  >"$WORK_DIR/both-discoveries.json" || exit 1
for mode in full limited; do
  start_test "$mode mode controls both distro and PowerShell discovery requests"
  arguments=()
  [ "$mode" != limited ] || arguments+=(--limit-testing)
  : >"$WORK_DIR/discovery-calls.txt"
  output="$(AZCLIPS_MATRIX_LIB_ONLY=1 bash -c '
    source "$1"
    calls_path="$2"
    shift 2
    get_docker_tags() { printf "docker\n" >>"$calls_path"; printf "3.25\n"; }
    get_github_tags() { printf "github\n" >>"$calls_path"; printf "v7.7.0\n"; }
    main "$@"
  ' _ "$SCRIPT_DIR/generate_ps_validation_matrix.sh" "$WORK_DIR/discovery-calls.txt" \
    --config "$WORK_DIR/both-discoveries.json" "${arguments[@]}")"
  assert_equals "0" "$?" "exit code"
  if [ "$mode" = limited ]; then
    assert_equals "" "$(cat "$WORK_DIR/discovery-calls.txt")" "neither discovery provider is called"
    assert_equals "alpine_3_24_ps7_6" \
      "$(jq -r 'keys | join(" ")' <<<"$output")" "only the configured alpha combination"
  else
    assert_equals $'github\ndocker' "$(cat "$WORK_DIR/discovery-calls.txt")" "full mode queries both providers"
    assert_equals "6" "$(jq -r 'length' <<<"$output")" "all configured and discovered combinations"
    assert_equals "4" "$(jq '[.[] | select(.ContinueOnError == "true")] | length' <<<"$output")" \
      "full mode retains all four canary combinations"
  fi
  end_test
done

start_test "limited mode does not depend on registry availability"
output="$(run_with_failing_registry --config "$WORK_DIR/alpha.json" --limit-testing \
  2>"$WORK_DIR/failure-stderr.txt")"
assert_equals "0" "$?" "exit code"
assert_equals "alpine_3_24_ps7_6" "$(jq -r 'keys | join(" ")' <<<"$output")" "configured alpha retained"
assert_equals "" "$(cat "$WORK_DIR/failure-stderr.txt")" "no discovery warnings in limited mode"
end_test

start_test "limited mode rejects an empty selection without emitting an Azure variable"
output="$(run_with_tags "" --config "$WORK_DIR/config.json" --skip-version-check --limit-testing \
  --azure-variable matrix 2>"$WORK_DIR/failure-stderr.txt")"
assert_equals "1" "$?" "exit code"
assert_equals "" "$output" "no empty Azure matrix"
assert_contains "$(cat "$WORK_DIR/failure-stderr.txt")" "--limit-testing selected no matrix legs" \
  "empty selection error"
end_test

start_test "limited mode rejects an empty selection without querying unavailable registries"
output="$(run_with_failing_registry --config "$WORK_DIR/config.json" --limit-testing \
  2>"$WORK_DIR/failure-stderr.txt")"
assert_equals "1" "$?" "exit code"
assert_equals "" "$output" "no empty JSON matrix"
assert_contains "$(cat "$WORK_DIR/failure-stderr.txt")" "--limit-testing selected no matrix legs" \
  "empty configured selection error"
assert_equals "0" "$(grep -c 'Unable to check' "$WORK_DIR/failure-stderr.txt")" \
  "no optional discovery warnings"
end_test

start_test "repository limited mode selects its existing Ubuntu alpha version only"
output="$(run_with_failing_registry --limit-testing 2>"$WORK_DIR/failure-stderr.txt")"
assert_equals "0" "$?" "exit code"
assert_equals "ubuntu_26_04_ps7_6" "$(jq -r 'keys | join(" ")' <<<"$output")" "existing alpha coverage unchanged"
assert_equals "" "$(cat "$WORK_DIR/failure-stderr.txt")" "repository limited mode skips all discovery"
end_test

start_test "help documents limit-testing as a switch"
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --help)"
assert_equals "0" "$?" "exit code"
assert_contains "$output" "--limit-testing        Select only configured TestInAlpha legs; disables version discovery." "switch usage"
end_test

start_test "limit-testing does not accept a boolean argument"
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" --limit-testing false \
  2>"$WORK_DIR/failure-stderr.txt")"
assert_equals "1" "$?" "exit code"
assert_equals "" "$output" "no output on invalid CLI use"
assert_contains "$(cat "$WORK_DIR/failure-stderr.txt")" "Unknown argument 'false'" "switch takes no value"
end_test

start_test "default YAML and equivalent JSON generate the same full and limited matrices"
for mode in full limited; do
  arguments=(--skip-version-check)
  [ "$mode" != limited ] || arguments+=(--limit-testing)
  yaml_output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" "${arguments[@]}")"
  assert_equals "0" "$?" "$mode YAML exit code"
  json_output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
    --config "$WORK_DIR/repository.json" "${arguments[@]}")"
  assert_equals "0" "$?" "$mode JSON exit code"
  assert_equals "$json_output" "$yaml_output" "$mode matrix parity"
done
end_test

start_test "supports yml files with comments and quoted version identifiers"
cp "$SCRIPT_DIR/ps_validation_matrix.yaml" "$WORK_DIR/matrix.yml" || exit 1
output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
  --config "$WORK_DIR/matrix.yml" --skip-version-check)"
assert_equals "0" "$?" "yml exit code"
assert_equals "10.0" "$(jq -r '.alpine_3_21_ps7_6.DotnetRuntimeVersion' <<<"$output")" "runtime string preserved"
assert_equals "true" "$(jq -r 'all(.[]; (.PowerShellVersion | type) == "string")' <<<"$output")" \
  "PowerShell versions remain strings"
end_test

start_test "YAML conversion preserves version discovery regular expressions"
python3 -c 'import json, sys, yaml; print(yaml.safe_dump(json.load(sys.stdin)))' \
  <"$WORK_DIR/config.json" >"$WORK_DIR/discovery.yaml" || exit 1
output="$(run_with_tags $'3.24\n3.25' --config "$WORK_DIR/discovery.yaml")"
assert_equals "0" "$?" "YAML discovery exit code"
assert_equals "alpine:3.25" "$(jq -r '.alpine_3_25_ps7_6_unsupported.ContainerImage' <<<"$output")" \
  "YAML regex discovers a canary"
end_test

for kind in malformed duplicate multidocument unsafe; do
  start_test "rejects $kind YAML without emitting a matrix"
  case "$kind" in
    malformed) printf 'schemaVersion: [\n' ;;
    duplicate) printf 'schemaVersion: 3\nschemaVersion: 4\n' ;;
    multidocument) printf 'schemaVersion: 3\n---\nschemaVersion: 3\n' ;;
    unsafe) printf '!!python/object:object {}\n' ;;
  esac >"$WORK_DIR/invalid.yaml"
  output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
    --config "$WORK_DIR/invalid.yaml" --azure-variable matrix 2>"$WORK_DIR/yaml-errors.txt")"
  assert_equals "1" "$?" "$kind YAML exit code"
  assert_equals "" "$output" "no partial or empty Azure matrix"
  assert_contains "$(cat "$WORK_DIR/yaml-errors.txt")" "Invalid matrix configuration" "YAML diagnostic"
  end_test
done

for kind in version boolean; do
  start_test "YAML scalar types still obey the matrix schema: $kind"
  if [ "$kind" = version ]; then
    filter='.powershell.versions[1].version = 7.6'
    expected='numeric version strings'
  else
    filter='.distributions[0].versions[0].testInAlpha = "false"'
    expected='must be a boolean'
  fi
  jq "$filter" "$WORK_DIR/config.json" |
    python3 -c 'import json, sys, yaml; print(yaml.safe_dump(json.load(sys.stdin)))' \
    >"$WORK_DIR/scalars.yaml" || exit 1
  output="$(bash "$SCRIPT_DIR/generate_ps_validation_matrix.sh" \
    --config "$WORK_DIR/scalars.yaml" --azure-variable matrix 2>"$WORK_DIR/yaml-errors.txt")"
  assert_equals "1" "$?" "incorrect scalar type fails"
  assert_equals "" "$output" "no matrix emitted"
  assert_contains "$(cat "$WORK_DIR/yaml-errors.txt")" "$expected" "schema diagnostic"
  end_test
done

printf '\n'
if [ "$failures" -eq 0 ]; then
  printf 'OK - %d tests passed\n' "$tests_run"
  exit 0
fi

printf 'FAILED - %d assertion failure(s) across %d tests\n' "$failures" "$tests_run" >&2
exit 1
