#!/usr/bin/env bash
# Generates the PowerShell validation matrix from ps_validation_matrix.json.
#
# Distributions either run inside a container on a Linux agent ("container" mode) or
# directly on a platform-specific agent such as macOS or Windows ("native" mode). Each
# distribution version is validated against every PowerShell version it is configured
# for, so a matrix leg is a distribution version and PowerShell version pair.
#
# Full runs include every configured version. When a distribution enables version
# checking, the newest release published to the container registry is added as an extra
# leg, and the same happens for the newest published PowerShell release. Those legs, and
# any PowerShell version the modules are known not to load on, are marked "(unsupported)"
# so that they report a warning instead of failing the build. Limited runs select only
# configured TestInAlpha versions and disable all version discovery.

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

DOCKER_AUTH_URL="https://auth.docker.io/token"
DOCKER_REGISTRY_URL="https://registry-1.docker.io"
GITHUB_API_URL="https://api.github.com"
VERSION_PATTERN='^[0-9]+(\.[0-9]+)*$'
DISTRIBUTION_ID_PATTERN='^[A-Za-z][A-Za-z0-9_]*$'
MATRIX_KEY_PATTERN='^[A-Za-z][A-Za-z0-9_]{0,99}$'

config_path="${SCRIPT_DIR}/ps_validation_matrix.json"
pool_override=""
pool_overrides=""
skip_version_check=0
limit_testing=0
azure_variable=""
warnings_file=""
az_module_version=""

usage() {
  cat <<'EOF'
Usage: generate_ps_validation_matrix.sh [options]

Options:
  --config PATH          Path to a JSON configuration file.
  --pool NAME            Override the agent pool used by container distributions.
  --pool-for ID=NAME     Override the agent pool for a single distribution id.
  --skip-version-check   Generate only configured versions without querying registries
                         or the PowerShell release feed.
  --limit-testing        Select only configured TestInAlpha legs; disables version discovery.
  --azure-variable NAME  Emit an Azure Pipelines output variable with this name.
  -h, --help             Show this help text.
EOF
}

fail() {
  printf 'ERROR: %s\n' "$1" >&2
  exit 1
}

# Resolves the Az roll-up module version so matrix legs can label the Azure PS
# release under test.
get_az_module_version() {
  local manifest="${SCRIPT_DIR}/../../../../src/PS/Az/Az.psd1"
  [ -f "$manifest" ] || fail "Az module manifest '$manifest' was not found."

  local version
  version="$(sed -n "s/^[[:space:]]*ModuleVersion[[:space:]]*=[[:space:]]*'\([^']*\)'.*/\1/p" "$manifest" |
    head -n 1 |
    tr -d '\r')" || fail "Unable to read Az module manifest '$manifest'."
  [ -n "$version" ] || fail "Unable to parse top-level ModuleVersion from Az module manifest '$manifest'."

  printf '%s' "$version"
}

# jq builds for Windows terminate lines with CRLF, which would otherwise leave a
# stray carriage return inside every value read from the configuration.
jq_run() {
  jq "$@" | tr -d '\r'
}

escape_azure_value() {
  local value="$1"
  value="${value//%/%AZP25}"
  value="${value//$'\r'/%0D}"
  value="${value//$'\n'/%0A}"
  printf '%s' "$value"
}

# Warnings are recorded rather than printed because the matrix is produced inside a
# command substitution, whose stdout would otherwise swallow the message.
warn() {
  if [ -n "$warnings_file" ]; then
    printf '%s\n' "$1" >>"$warnings_file"
  else
    printf 'WARNING: %s\n' "$1" >&2
  fi
}

flush_warnings() {
  [ -n "$warnings_file" ] && [ -s "$warnings_file" ] || return 0

  local message
  while IFS= read -r message; do
    [ -n "$message" ] || continue
    if [ -n "$azure_variable" ]; then
      printf '##vso[task.logissue type=warning]%s\n' "$(escape_azure_value "$message")"
    else
      printf 'WARNING: %s\n' "$message" >&2
    fi
  done <"$warnings_file"
}

parse_args() {
  while [ "$#" -gt 0 ]; do
    case "$1" in
      --config)
        [ "$#" -ge 2 ] || fail "--config requires a value."
        config_path="$2"
        shift 2
        ;;
      --pool)
        [ "$#" -ge 2 ] || fail "--pool requires a value."
        pool_override="$2"
        shift 2
        ;;
      --pool-for)
        [ "$#" -ge 2 ] || fail "--pool-for requires a value."
        case "$2" in
          *=*) ;;
          *) fail "--pool-for expects ID=POOL." ;;
        esac
        { [ -n "${2%%=*}" ] && [ -n "${2#*=}" ]; } ||
          fail "--pool-for expects a non-empty distribution id and pool name."
        pool_overrides="${pool_overrides}${2}"$'\n'
        shift 2
        ;;
      --skip-version-check)
        skip_version_check=1
        shift
        ;;
      --limit-testing)
        limit_testing=1
        skip_version_check=1
        shift
        ;;
      --azure-variable)
        [ "$#" -ge 2 ] || fail "--azure-variable requires a value."
        azure_variable="$2"
        shift 2
        ;;
      -h|--help)
        usage
        exit 0
        ;;
      *)
        fail "Unknown argument '$1'."
        ;;
    esac
  done
}

require_dependencies() {
  local dependency
  for dependency in jq curl sort tr mktemp; do
    command -v "$dependency" >/dev/null 2>&1 ||
      fail "'$dependency' is required but was not found on PATH."
  done
}

# Returns 0 when the first version sorts strictly above the second.
version_gt() {
  [ "$1" != "$2" ] || return 1
  local latest
  latest="$(printf '%s\n%s\n' "$1" "$2" | sort -V | tail -n 1)" || return 2
  [ "$latest" = "$1" ]
}

get_matrix_key() {
  local distribution_id="$1" version="$2" powershell_version="$3" unsupported="$4"
  local normalized_version="${version//[^A-Za-z0-9_]/_}"
  local normalized_powershell="${powershell_version//[^A-Za-z0-9_]/_}"
  local suffix=""
  [ "$unsupported" = "true" ] && suffix="_unsupported"
  printf '%s_%s_ps%s%s' "$distribution_id" "$normalized_version" "$normalized_powershell" "$suffix"
}

# Prints the PowerShell versions a distribution is validated against.
get_powershell_versions() {
  local distribution="$1" has_versions
  has_versions="$(jq_run -r 'has("powershellVersions")' <<<"$distribution")" || return 1
  if [ "$has_versions" = "true" ]; then
    jq_run -r '.powershellVersions[]' <<<"$distribution"
  else
    jq_run -r '.powershell.defaultVersions[]' "$config_path"
  fi
}

# Prints the .NET runtime a configured PowerShell version is built against.
get_dotnet_runtime() {
  jq_run -r --arg version "$1" \
    '.powershell.versions[] | select(.version == $version) | .dotnetRuntime // empty' "$config_path"
}

get_powershell_support() {
  # '.supported // true' cannot be used because jq treats false as an absent value.
  jq_run -r --arg version "$1" \
    '[.powershell.versions[] | select(.version == $version)]
     | if length == 0 then false
       else .[0] | if has("supported") then .supported else true end end' "$config_path"
}

# PowerShell 7.x ships on .NET (x + 4). Only used for versions discovered from the
# release feed, which by definition are not described by the configuration yet.
derive_dotnet_runtime() {
  local version="$1" major minor
  major="${version%%.*}"
  minor="${version#*.}"
  minor="${minor%%.*}"
  [ "$major" = "7" ] || return 0
  printf '%s.0' "$((10#$minor + 4))"
}

get_execution_mode() {
  jq_run -r '.executionMode // "container"' <<<"$1"
}

# Prints the repository-relative bootstrap script path using the flattened
# platform script naming convention.
get_distribution_script() {
  local distribution_id="$1" family="$2"
  case "$family" in
    windows)
      printf '%s' 'devops/scripts/packaging/powershell/windows/windows_install_script.ps1'
      ;;
    macos)
      printf '%s' 'devops/scripts/packaging/powershell/macos/macos_install_script.sh'
      ;;
    *)
      printf '%s' "devops/scripts/packaging/powershell/linux/${distribution_id}_install_script.sh"
      ;;
  esac
}

# Resolves a --pool-for override for the given distribution id.
lookup_pool_override() {
  local distribution_id="$1" entry
  while IFS= read -r entry; do
    [ -n "$entry" ] || continue
    case "$entry" in
      "${distribution_id}="*)
        printf '%s' "${entry#*=}"
        return 0
        ;;
    esac
  done <<<"$pool_overrides"
  return 1
}

# Validate types before any shell loops so malformed collections cannot silently
# disappear through jq failures or be mistaken for optional/defaulted values.
validate_config_shape() {
  jq_run -e -s --arg version_pattern "$VERSION_PATTERN" '
    def check($condition; $message):
      if $condition then . else error($message) end;
    def text: type == "string" and test("\\S");
    def version: type == "string" and test($version_pattern);
    def nonempty_array: type == "array" and length > 0;
    def optional_bool($key; $label):
      check((has($key) | not) or (.[$key] | type == "boolean");
        $label + "." + $key + " must be a boolean.");
    def optional_text($key; $label):
      check((has($key) | not) or (.[$key] | text);
        $label + "." + $key + " must be a nonempty string.");
    def version_check($label; $sources):
      check(type == "object"; $label + " must be an object.")
      | check(.enabled | type == "boolean"; $label + ".enabled must be a boolean.")
      | optional_text("source"; $label)
      | optional_text("repository"; $label)
      | optional_text("tagPattern"; $label)
      | check((has("source") | not) or (.source as $s | $sources | index($s) != null);
          $label + " has an unsupported source.")
      | check((.enabled | not) or ((.repository | text) and (.tagPattern | text));
          $label + " must configure a repository and tagPattern when enabled.");
    check(length == 1; "The configuration must contain exactly one JSON object.")
    | .[0]
    | check(type == "object"; "The configuration must be an object.")
    | check(.schemaVersion == 3; "Only schemaVersion 3 is supported.")
    | .powershell |= (
        check(type == "object"; "powershell must be an object.")
        | check(.versions | nonempty_array; "powershell.versions must be a nonempty array.")
        | check(.defaultVersions | nonempty_array; "powershell.defaultVersions must be a nonempty array.")
        | check(all(.defaultVersions[]; version); "Default PowerShell versions must be numeric version strings.")
        | .versions |= map(
            check(type == "object"; "PowerShell version entries must be objects.")
            | check(.version | version; "PowerShell versions must be numeric version strings.")
            | check(.dotnetRuntime | version; "Each PowerShell version must declare the .NET runtime as a numeric version string.")
            | optional_bool("supported"; "PowerShell version"))
        | if has("versionCheck") then
            .versionCheck |= version_check("PowerShell versionCheck"; ["dockerhub", "github"])
          else . end)
    | check(.distributions | nonempty_array; "distributions must be a nonempty array.")
    | .distributions |= map(
        check(type == "object"; "Distribution entries must be objects.")
        | check(.id | text; "Distribution id must be a nonempty string.")
        | .id as $id
        | check(.title | text; "Distribution " + $id + ".title must be a nonempty string.")
        | check(.family | text; "Distribution " + $id + ".family must be a nonempty string.")
        | optional_text("executionMode"; $id)
        | optional_text("pool"; $id)
        | optional_text("poolTemplate"; $id)
        | optional_text("imageTemplate"; $id)
        | check((has("titleSuffix") | not) or (.titleSuffix | type == "string");
            $id + ".titleSuffix must be a string.")
        | optional_bool("powershellVersionCheck"; $id)
        | check(.versions | nonempty_array; $id + ".versions must be a nonempty array.")
        | (.family == "windows" and .executionMode == "native") as $native_windows
        | .versions |= map(
            check(type == "object"; $id + " version entries must be objects.")
            | check((.version | version) or ($native_windows and .version == "latest");
                $id + " versions must be numeric version strings (or \"latest\" for native Windows).")
            | optional_bool("testInAlpha"; $id + " version"))
        | if has("powershellVersions") then
            check(.powershellVersions | nonempty_array; $id + ".powershellVersions must be a nonempty array.")
            | check(all(.powershellVersions[]; version); $id + ".powershellVersions must contain numeric version strings.")
          else . end
        | .versionCheck |= version_check($id + " versionCheck"; ["dockerhub"]))
    | true
  ' "$config_path" >/dev/null || fail "Invalid matrix configuration '$config_path'."
}

validate_tag_pattern() {
  local tag_pattern="$1" label="$2"
  case "$tag_pattern" in
    *"("*) ;;
    *) fail "$label tagPattern must contain a capture group for the version." ;;
  esac
  if [[ "" =~ $tag_pattern ]]; then
    return 0
  else
    [ "$?" -eq 1 ] || fail "$label tagPattern is not a valid regular expression."
  fi
}

validate_powershell_config() {
  local duplicate
  duplicate="$(jq_run -r '[.powershell.versions[].version] | group_by(.) | map(select(length > 1) | .[0]) | join(", ")' "$config_path")" || return 1
  [ -z "$duplicate" ] || fail "Duplicate PowerShell version '$duplicate' in the configuration."

  local undeclared
  undeclared="$(jq_run -r \
    '[.powershell.versions[].version] as $declared
     | [.powershell.defaultVersions[] | . as $version | select($declared | index($version) | not)]
     | join(", ")' \
    "$config_path")" || return 1
  [ -z "$undeclared" ] ||
    fail "Default PowerShell version '$undeclared' is not declared in the powershell.versions list."

  local enabled
  enabled="$(jq_run -r '.powershell.versionCheck.enabled // false' "$config_path")" || return 1
  [ "$enabled" = "true" ] || return 0

  local tag_pattern
  tag_pattern="$(jq_run -r '.powershell.versionCheck.tagPattern' "$config_path")" || return 1
  validate_tag_pattern "$tag_pattern" "The PowerShell versionCheck"
}

validate_config() {
  validate_config_shape || return 1

  validate_powershell_config || return 1

  local distribution_count duplicate
  distribution_count="$(jq_run -r '.distributions | length' "$config_path")" || return 1
  duplicate="$(jq_run -r '[.distributions[].id] | group_by(.) | map(select(length > 1) | .[0]) | join(", ")' "$config_path")" || return 1
  [ -z "$duplicate" ] || fail "Duplicate distribution id '$duplicate'."

  local matrix_keys=" "
  local index=0
  while [ "$index" -lt "$distribution_count" ]; do
    local distribution
    distribution="$(jq_run -c ".distributions[$index]" "$config_path")" || return 1

    local distribution_id
    distribution_id="$(jq_run -r '.id // empty' <<<"$distribution")" || return 1
    [ -n "$distribution_id" ] ||
      fail "Distribution at index $index is missing required property 'id'."
    [[ "$distribution_id" =~ $DISTRIBUTION_ID_PATTERN ]] ||
      fail "Distribution id '$distribution_id' must start with a letter and contain only letters, digits, and underscores."

    local execution_mode
    execution_mode="$(get_execution_mode "$distribution")" || return 1
    case "$execution_mode" in
      container|native) ;;
      *) fail "Distribution '$distribution_id' has unsupported executionMode '$execution_mode'; expected 'container' or 'native'." ;;
    esac

    local family distribution_script
    family="$(jq_run -r '.family' <<<"$distribution")" || return 1
    case "$family:$execution_mode" in
      alpine:container|debian:container|rhel:container|macos:native|windows:native) ;;
      *) fail "Distribution '$distribution_id' has unsupported family/executionMode combination '$family/$execution_mode'." ;;
    esac
    distribution_script="$(get_distribution_script "$distribution_id" "$family")" || return 1
    [ -f "${SCRIPT_DIR}/../../../../${distribution_script}" ] ||
      fail "DistributionScript '$distribution_script' for '$distribution_id' was not found."

    # Container legs need an image to pull, native legs need a platform-specific pool.
    local required_properties="title family versions versionCheck"
    if [ "$execution_mode" = "container" ]; then
      required_properties="$required_properties pool imageTemplate"
    else
      required_properties="$required_properties poolTemplate"
    fi

    local property has_property
    for property in $required_properties; do
      has_property="$(jq_run -r --arg p "$property" 'has($p)' <<<"$distribution")" || return 1
      if [ "$has_property" != "true" ]; then
        fail "Distribution '$distribution_id' with executionMode '$execution_mode' is missing required property '$property'."
      fi
    done

    local version_check_enabled
    version_check_enabled="$(jq_run -r '.versionCheck.enabled' <<<"$distribution")" || return 1
    if [ "$execution_mode" = "native" ] && [ "$version_check_enabled" = "true" ]; then
      fail "Distribution '$distribution_id' cannot enable version checking because container registry discovery does not apply to native agents."
    fi

    local powershell_versions
    powershell_versions="$(get_powershell_versions "$distribution")" || return 1
    [ -n "$powershell_versions" ] ||
      fail "Distribution '$distribution_id' must be validated against at least one PowerShell version."

    local powershell_version dotnet_runtime
    while IFS= read -r powershell_version; do
      [ -n "$powershell_version" ] || continue
      dotnet_runtime="$(get_dotnet_runtime "$powershell_version")" || return 1
      [ -n "$dotnet_runtime" ] ||
        fail "PowerShell version '$powershell_version' referenced by '$distribution_id' is not declared in the powershell.versions list."
    done <<<"$powershell_versions"

    local version versions
    versions="$(jq_run -r '.versions[].version' <<<"$distribution")" || return 1
    while IFS= read -r version; do
      [[ "$version" =~ $VERSION_PATTERN || "$family:$execution_mode:$version" == "windows:native:latest" ]] ||
        fail "Version '$version' of '$distribution_id' must contain only numeric components, except 'latest' for native Windows."

      while IFS= read -r powershell_version; do
        [ -n "$powershell_version" ] || continue

        local matrix_key unsupported_key
        matrix_key="$(get_matrix_key "$distribution_id" "$version" "$powershell_version" false)" || return 1
        unsupported_key="$(get_matrix_key "$distribution_id" "$version" "$powershell_version" true)" || return 1
        [[ "$unsupported_key" =~ $MATRIX_KEY_PATTERN ]] ||
          fail "Matrix key '$matrix_key' is not a valid Azure Pipelines job name."

        case "$matrix_keys" in
          *" $matrix_key "*) fail "Duplicate matrix key '$matrix_key'." ;;
        esac
        matrix_keys="${matrix_keys}${matrix_key} "
      done <<<"$powershell_versions"
    done <<<"$versions"

    if [ "$version_check_enabled" = "true" ]; then
      local tag_pattern
      tag_pattern="$(jq_run -r '.versionCheck.tagPattern' <<<"$distribution")" || return 1
      validate_tag_pattern "$tag_pattern" "Distribution '$distribution_id'" || return 1
    fi

    index=$((index + 1))
  done
}

# Overridable so that tests can supply tags without network access.
get_docker_tags() {
  local repository="$1"
  local token

  token="$(curl -fsSL --max-time 30 \
    "${DOCKER_AUTH_URL}?service=registry.docker.io&scope=repository:${repository}:pull" |
    jq_run -r '.token // empty')" || return 1
  [ -n "$token" ] || return 1

  curl -fsSL --max-time 30 \
    -H "Authorization: Bearer ${token}" \
    "${DOCKER_REGISTRY_URL}/v2/${repository}/tags/list?n=10000" |
    jq_run -r '.tags[]? // empty'
}

# Overridable so that tests can supply tags without network access.
get_github_tags() {
  local repository="$1"

  curl -fsSL --max-time 30 \
    -H 'Accept: application/vnd.github+json' \
    -H 'X-GitHub-Api-Version: 2022-11-28' \
    "${GITHUB_API_URL}/repos/${repository}/releases?per_page=100" |
    jq_run -r '.[]? | select((.draft | not) and (.prerelease | not)) | .tag_name // empty'
}

get_registry_tags() {
  local source="$1" repository="$2"
  case "$source" in
    dockerhub) get_docker_tags "$repository" ;;
    github) get_github_tags "$repository" ;;
    *) return 1 ;;
  esac
}

# Prints the newest published version above every configured version, if any.
find_new_version() {
  local tag_pattern="$1" latest_configured="$2" configured_versions="$3" tags="$4"
  local tag candidate best=""

  while IFS= read -r tag; do
    [ -n "$tag" ] || continue
    [[ "$tag" =~ $tag_pattern ]] || continue
    candidate="${BASH_REMATCH[1]:-}"
    [ -n "$candidate" ] || continue
    [[ "$candidate" =~ $VERSION_PATTERN ]] || continue
    case "$configured_versions" in
      *" $candidate "*) continue ;;
    esac
    if version_gt "$candidate" "$latest_configured"; then
      :
    else
      [ "$?" -eq 1 ] || return 1
      continue
    fi
    if [ -z "$best" ]; then
      best="$candidate"
    elif version_gt "$candidate" "$best"; then
      best="$candidate"
    else
      [ "$?" -eq 1 ] || return 1
    fi
  done <<<"$tags"

  printf '%s' "$best"
}

emit_entry() {
  local distribution="$1" version="$2" powershell_version="$3" test_in_alpha="$4" unsupported="$5"
  local distribution_id title title_suffix family execution_mode image pool override
  local dotnet_runtime distribution_script matrix_key
  distribution_id="$(jq_run -r '.id' <<<"$distribution")" || return 1
  title="$(jq_run -r '.title' <<<"$distribution")" || return 1
  title_suffix="$(jq_run -r '.titleSuffix // ""' <<<"$distribution")" || return 1
  family="$(jq_run -r '.family' <<<"$distribution")" || return 1
  execution_mode="$(get_execution_mode "$distribution")" || return 1
  distribution_script="$(get_distribution_script "$distribution_id" "$family")" || return 1

  dotnet_runtime="$(get_dotnet_runtime "$powershell_version")" || return 1
  if [ -z "$dotnet_runtime" ]; then
    dotnet_runtime="$(derive_dotnet_runtime "$powershell_version")" || return 1
  fi
  [[ "$dotnet_runtime" =~ $VERSION_PATTERN ]] ||
    fail "Unable to resolve a .NET runtime for PowerShell '$powershell_version'."

  if [ "$execution_mode" = "container" ]; then
    local image_template
    image_template="$(jq_run -r '.imageTemplate' <<<"$distribution")" || return 1
    image="${image_template//\{version\}/$version}"
    # --pool targets the agents that host the containers, so it must never be applied
    # to native distributions that require a platform-specific pool.
    pool="$(jq_run -r '.pool' <<<"$distribution")" || return 1
    pool="${pool_override:-$pool}"
  else
    local pool_template
    pool_template="$(jq_run -r '.poolTemplate' <<<"$distribution")" || return 1
    image=""
    pool="${pool_template//\{version\}/$version}"
  fi

  if override="$(lookup_pool_override "$distribution_id")"; then
    pool="$override"
  fi

  # Shared pipeline configuration uses pool-* for named pools, not hosted images.
  local pool_name="Azure Pipelines" vm_image="$pool"
  if [[ "${pool,,}" == pool-* ]]; then
    pool_name="$pool"
    vm_image=""
  fi

  local unsupported_suffix=""
  [ "$unsupported" = "true" ] && unsupported_suffix=" (unsupported)"
  matrix_key="$(get_matrix_key "$distribution_id" "$version" "$powershell_version" "$unsupported")" || return 1
  [[ "$matrix_key" =~ $MATRIX_KEY_PATTERN ]] ||
    fail "Matrix key '$matrix_key' is not a valid Azure Pipelines job name."

  jq_run -n -c -e \
    --arg key "$matrix_key" \
    --arg id "$distribution_id" \
    --arg name "${title} ${version}${title_suffix} on PowerShell ${powershell_version}${unsupported_suffix}" \
    --arg image "$image" \
    --arg family "$family" \
    --arg script "$distribution_script" \
    --arg mode "$execution_mode" \
    --arg pool "$pool" \
    --arg pool_name "$pool_name" \
    --arg vm_image "$vm_image" \
    --arg powershell "$powershell_version" \
    --arg az "$az_module_version" \
    --arg runtime "$dotnet_runtime" \
    --arg alpha "$test_in_alpha" \
    --arg soft "$unsupported" \
    '{($key): {
        ValidationKey: $key,
        DistributionId: $id,
        DistributionName: $name,
        ContainerImage: $image,
        DistributionFamily: $family,
        DistributionScript: $script,
        DistributionPool: $pool,
        DistributionPoolName: $pool_name,
        DistributionVmImage: $vm_image,
        ValidationMode: $mode,
        PowerShellVersion: $powershell,
        AzVersion: $az,
        DotnetRuntimeVersion: $runtime,
        TestInAlpha: $alpha,
        ContinueOnError: $soft
      }}'
}

# Prints the newest published PowerShell version above every configured version, if any.
# Discovery runs once for the whole matrix rather than per distribution.
discover_powershell_version() {
  [ "$skip_version_check" -eq 0 ] || return 0
  local enabled opted_in
  enabled="$(jq_run -r '.powershell.versionCheck.enabled // false' "$config_path")" || return 1
  [ "$enabled" = "true" ] || return 0
  opted_in="$(jq_run -r '[.distributions[] | select(.powershellVersionCheck == true)] | length' "$config_path")" || return 1
  [ "$opted_in" -gt 0 ] || return 0

  local source repository tag_pattern configured_versions latest_configured tags
  source="$(jq_run -r '.powershell.versionCheck.source // "dockerhub"' "$config_path")" || return 1
  repository="$(jq_run -r '.powershell.versionCheck.repository' "$config_path")" || return 1
  tag_pattern="$(jq_run -r '.powershell.versionCheck.tagPattern' "$config_path")" || return 1

  configured_versions="$(jq_run -r '[.powershell.versions[].version] | join(" ")' "$config_path")" || return 1
  configured_versions=" $configured_versions "
  latest_configured="$(jq_run -r '.powershell.versions[].version' "$config_path" | sort -V | tail -n 1)" || return 1

  # Discovery must never block validation of the supported PowerShell versions.
  if ! tags="$(get_registry_tags "$source" "$repository" 2>/dev/null)"; then
    warn "Unable to check for new PowerShell versions, keeping the configured versions only." || return 1
    return 0
  fi

  find_new_version "$tag_pattern" "$latest_configured" "$configured_versions" "$tags"
}

build_matrix() {
  local entries=""
  local distribution_count index=0
  distribution_count="$(jq_run -r '.distributions | length' "$config_path")" || return 1

  local discovered_powershell
  discovered_powershell="$(discover_powershell_version)" || return 1

  while [ "$index" -lt "$distribution_count" ]; do
    local distribution distribution_id powershell_versions powershell_check_enabled
    distribution="$(jq_run -c ".distributions[$index]" "$config_path")" || return 1
    distribution_id="$(jq_run -r '.id' <<<"$distribution")" || return 1
    powershell_versions="$(get_powershell_versions "$distribution")" || return 1
    powershell_check_enabled="$(jq_run -r '.powershellVersionCheck // false' <<<"$distribution")" || return 1

    # A newly released PowerShell version is a canary: it is never known to work yet.
    if [ -n "$discovered_powershell" ] && [ "$powershell_check_enabled" = "true" ]; then
      powershell_versions="${powershell_versions}"$'\n'"${discovered_powershell}"
    fi

    local configured_versions=" " latest_configured="" version test_in_alpha powershell_version versions entry
    versions="$(jq_run -r '.versions[] | [.version, (.testInAlpha // false | tostring)] | @tsv' <<<"$distribution")" || return 1
    while IFS=$'\t' read -r version test_in_alpha; do
      while IFS= read -r powershell_version; do
        [ -n "$powershell_version" ] || continue

        local powershell_supported powershell_unsupported=false alpha="$test_in_alpha"
        powershell_supported="$(get_powershell_support "$powershell_version")" || return 1
        [ "$powershell_supported" = "true" ] || powershell_unsupported=true
        [ "$powershell_version" != "$discovered_powershell" ] || alpha=true
        entry="$(emit_entry "$distribution" "$version" "$powershell_version" \
          "$alpha" "$powershell_unsupported")" || return 1
        [ -n "$entry" ] || fail "Matrix generation must produce nonempty entries."
        entries+="$entry"$'\n'
      done <<<"$powershell_versions"

      configured_versions="${configured_versions}${version} "
      # Hosted-agent aliases do not participate in numeric release discovery.
      [[ "$version" =~ $VERSION_PATTERN ]] || continue
      if [ -z "$latest_configured" ]; then
        latest_configured="$version"
      elif version_gt "$version" "$latest_configured"; then
        latest_configured="$version"
      else
        [ "$?" -eq 1 ] || return 1
      fi
    done <<<"$versions"

    index=$((index + 1))

    [ "$skip_version_check" -eq 0 ] || continue
    local version_check_enabled
    version_check_enabled="$(jq_run -r '.versionCheck.enabled' <<<"$distribution")" || return 1
    [ "$version_check_enabled" = "true" ] || continue

    local repository tag_pattern tags new_version
    repository="$(jq_run -r '.versionCheck.repository' <<<"$distribution")" || return 1
    tag_pattern="$(jq_run -r '.versionCheck.tagPattern' <<<"$distribution")" || return 1

    # Discovery must never block validation of the supported distributions.
    if ! tags="$(get_docker_tags "$repository" 2>/dev/null)"; then
      warn "Unable to check for new '${distribution_id}' versions, keeping the configured versions only." || return 1
      continue
    fi

    new_version="$(find_new_version "$tag_pattern" "$latest_configured" "$configured_versions" "$tags")" || return 1
    if [ -n "$new_version" ]; then
      while IFS= read -r powershell_version; do
        [ -n "$powershell_version" ] || continue
        entry="$(emit_entry "$distribution" "$new_version" "$powershell_version" true true)" || return 1
        [ -n "$entry" ] || fail "Matrix generation must produce nonempty entries."
        entries+="$entry"$'\n'
      done <<<"$powershell_versions"
    fi
  done

  printf '%s' "$entries" | jq_run -s -c -e '
    if length == 0 or any(.[]; type != "object" or length != 1)
    then error("Matrix generation must produce nonempty entries.")
    else add end'
}

main() {
  parse_args "$@"
  require_dependencies
  [ -f "$config_path" ] || fail "Configuration file '$config_path' was not found."
  validate_config || fail "Unable to validate the matrix configuration."
  az_module_version="$(get_az_module_version)" || fail "Unable to resolve the Az module version."

  warnings_file="$(mktemp)" || fail "Unable to create the warnings file."
  trap 'rm -f -- "$warnings_file"' EXIT

  local matrix_json
  matrix_json="$(build_matrix)" || fail "Unable to generate the validation matrix."

  flush_warnings || fail "Unable to report matrix discovery warnings."

  if [ "$limit_testing" -eq 1 ]; then
    matrix_json="$(jq_run -c -e '
      with_entries(select(.value.TestInAlpha == "true"))
      | if length == 0 then error("--limit-testing selected no matrix legs.") else . end
    ' <<<"$matrix_json")" || fail "Unable to generate a nonempty limited validation matrix."
  fi

  if [ -n "$azure_variable" ]; then
    printf '##vso[task.setvariable variable=%s;isOutput=true]%s\n' \
      "$azure_variable" "$(escape_azure_value "$matrix_json")"
  else
    printf '%s\n' "$matrix_json"
  fi
}

if [ "${AZCLIPS_MATRIX_LIB_ONLY:-0}" != "1" ]; then
  main "$@"
fi
