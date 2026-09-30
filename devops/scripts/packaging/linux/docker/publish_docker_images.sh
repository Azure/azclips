#!/usr/bin/env bash
set -euo pipefail

ACTION="${ACTION:-publish}"
VERSION="${VERSION:?ERROR: VERSION is required}"
CLI_REGISTRY_NAME="${CLI_REGISTRY_NAME:?ERROR: CLI_REGISTRY_NAME is required}"
CLI_REGISTRY_SERVER="${CLI_REGISTRY_SERVER:?ERROR: CLI_REGISTRY_SERVER is required}"
CLI_REPOSITORY="${CLI_REPOSITORY:-public/azure-cli-v3}"
POWERSHELL_REGISTRY_NAME="${POWERSHELL_REGISTRY_NAME:?ERROR: POWERSHELL_REGISTRY_NAME is required}"
POWERSHELL_REGISTRY_SERVER="${POWERSHELL_REGISTRY_SERVER:?ERROR: POWERSHELL_REGISTRY_SERVER is required}"
POWERSHELL_REPOSITORY="${POWERSHELL_REPOSITORY:-public/azure-powershell-v3}"
ARTIFACT_ROOT="${ARTIFACT_ROOT:-}"
METADATA_ROOT="${METADATA_ROOT:-}"

if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]]; then
  echo "ERROR: VERSION must be SemVer-like. Got: '$VERSION'."
  exit 1
fi

validate_registry_config() {
  local registry_name="$1"
  local registry_server="$2"
  local repository="$3"

  if [[ ! "$registry_name" =~ ^[a-z0-9]{5,50}$ ]]; then
    echo "ERROR: Invalid Azure Container Registry name '$registry_name'."
    exit 1
  fi
  if [[ "$registry_server" != "$registry_name.azurecr.io" ]]; then
    echo "ERROR: Registry server '$registry_server' does not match registry '$registry_name'."
    exit 1
  fi
  if [[ ! "$repository" =~ ^[a-z0-9]+([._-][a-z0-9]+)*(\/[a-z0-9]+([._-][a-z0-9]+)*)*$ ]]; then
    echo "ERROR: Invalid container repository '$repository'."
    exit 1
  fi
}

validate_registry_config "$CLI_REGISTRY_NAME" "$CLI_REGISTRY_SERVER" "$CLI_REPOSITORY"
validate_registry_config "$POWERSHELL_REGISTRY_NAME" "$POWERSHELL_REGISTRY_SERVER" "$POWERSHELL_REPOSITORY"

if [[ "$ACTION" == "validate" ]]; then
  exit 0
fi

use_registry() {
  REGISTRY_NAME="$1"
  REGISTRY_SERVER="$2"
}

tag_digest() {
  local repository="$1"
  local tag="$2"
  az acr repository show \
    --name "$REGISTRY_NAME" \
    --image "$repository:$tag" \
    --query digest \
    --output tsv
}

tag_exists() {
  local repository="$1"
  local tag="$2"
  local repository_count tag_count
  repository_count="$(az acr repository list \
    --name "$REGISTRY_NAME" \
    --query "[?@=='$repository'] | length(@)" \
    --output tsv)"
  if [[ "$repository_count" == "0" ]]; then
    return 1
  fi
  if [[ "$repository_count" != "1" ]]; then
    echo "ERROR: Unexpected repository lookup result for '$repository': '$repository_count'."
    exit 1
  fi

  tag_count="$(az acr repository show-tags \
    --name "$REGISTRY_NAME" \
    --repository "$repository" \
    --query "[?@=='$tag'] | length(@)" \
    --output tsv)"
  if [[ "$tag_count" == "1" ]]; then
    return 0
  fi
  if [[ "$tag_count" == "0" ]]; then
    return 1
  fi
  echo "ERROR: Unexpected tag lookup result for '$repository:$tag': '$tag_count'."
  exit 1
}

lock_versioned_tag() {
  local repository="$1"
  local tag="$2"
  az acr repository update \
    --name "$REGISTRY_NAME" \
    --image "$repository:$tag" \
    --write-enabled false \
    --delete-enabled false \
    --output none
}

lock_new_versioned_tag() {
  local repository="$1"
  local tag="$2"
  local image="$repository:$tag"

  if lock_versioned_tag "$repository" "$tag"; then
    return 0
  fi

  echo "ERROR: Failed to lock newly published image '$REGISTRY_SERVER/$image'. Removing only the new tag." >&2
  local tag_count=""
  if az acr repository untag \
    --name "$REGISTRY_NAME" \
    --image "$image"; then
    tag_count="$(az acr repository show-tags \
      --name "$REGISTRY_NAME" \
      --repository "$repository" \
      --query "[?@=='$tag'] | length(@)" \
      --output tsv 2>/dev/null || true)"
  fi
  if [[ "$tag_count" == "0" ]]; then
    echo "Removed unlocked version tag '$REGISTRY_SERVER/$image'." >&2
    return 1
  fi

  echo "WARNING: Failed to remove '$REGISTRY_SERVER/$image'. Retrying the immutable lock." >&2
  lock_versioned_tag "$repository" "$tag" >/dev/null 2>&1 || true

  local -a attributes=()
  local write_enabled delete_enabled
  mapfile -t attributes < <(az acr repository show \
    --name "$REGISTRY_NAME" \
    --image "$image" \
    --query '[writeEnabled, deleteEnabled]' \
    --output tsv 2>/dev/null || true)
  write_enabled="${attributes[0]:-}"
  delete_enabled="${attributes[1]:-}"
  if [[ "$write_enabled" == "false" && "$delete_enabled" == "false" ]]; then
    echo "Confirmed '$REGISTRY_SERVER/$image' is write- and delete-disabled." >&2
    return 1
  fi

  echo "CRITICAL: Unable to remove or confirm immutability for '$REGISTRY_SERVER/$image'. Manual remediation is required." >&2
  return 1
}

record_digest() {
  local repository="$1"
  local tag="$2"
  local digest
  digest="$(tag_digest "$repository" "$tag")"
  if [[ ! "$digest" =~ ^sha256:[0-9a-f]{64}$ ]]; then
    echo "ERROR: Registry returned an invalid digest for '$REGISTRY_SERVER/$repository:$tag': '$digest'."
    exit 1
  fi
  printf '%s/%s:%s@%s\n' "$REGISTRY_SERVER" "$repository" "$tag" "$digest" | tee -a "$digest_file"
}

create_manifest() {
  local repository="$1"
  local target_tag="$2"
  shift 2

  local target="$REGISTRY_SERVER/$repository:$target_tag"
  local sources=()
  local source_tag
  for source_tag in "$@"; do
    sources+=("$REGISTRY_SERVER/$repository:$source_tag")
  done

  docker buildx imagetools create --tag "$target" "${sources[@]}"
  docker buildx imagetools inspect "$target"
}

if [[ "$ACTION" == "promote" ]]; then
  if [[ -z "$METADATA_ROOT" || ! -f "$METADATA_ROOT/image-digests.txt" ]]; then
    echo "ERROR: Promotion requires the publication image-digests.txt metadata."
    exit 1
  fi
  digest_file="$METADATA_ROOT/image-digests.txt"

  reject_older_promotion() {
    local repository="$1"
    local alias_tag="$2"
    local version_suffix="$3"
    local current_digest current_tags current_version newest
    current_digest="$(az acr repository show-tags \
      --name "$REGISTRY_NAME" \
      --repository "$repository" \
      --detail \
      --query "[?name=='$alias_tag'].digest | [0]" \
      --output tsv)"
    [[ -n "$current_digest" ]] || return 0

    current_tags="$(az acr repository show-tags \
      --name "$REGISTRY_NAME" \
      --repository "$repository" \
      --detail \
      --query "[?digest=='$current_digest'].name" \
      --output tsv)"
    current_version="$(printf '%s\n' "$current_tags" \
      | grep -E "^[0-9]+\.[0-9]+\.[0-9]+-${version_suffix}$" \
      | sed "s/-${version_suffix}$//" \
      | sort -V \
      | tail -n 1 || true)"
    [[ -n "$current_version" ]] || return 0

    newest="$(printf '%s\n%s\n' "$current_version" "$VERSION" | sort -V | tail -n 1)"
    if [[ "$newest" != "$VERSION" ]]; then
      echo "ERROR: Refusing to promote version '$VERSION' over newer '$current_version' for '$repository:$alias_tag'."
      exit 1
    fi
  }

  published_ref() {
    local repository="$1"
    local tag="$2"
    local prefix="$REGISTRY_SERVER/$repository:$tag@"
    local matches
    matches="$(grep -F "$prefix" "$digest_file" || true)"
    if [[ "$(grep -c . <<<"$matches")" -ne 1 ]]; then
      echo "ERROR: Expected one digest record for '$prefix', found:" >&2
      printf '%s\n' "$matches" >&2
      return 1
    fi
    printf '%s\n' "$matches"
  }

  promote_alias() {
    local repository="$1"
    local alias_tag="$2"
    local version_suffix="$3"
    local source_ref previous_digest=""
    reject_older_promotion "$repository" "$alias_tag" "$version_suffix"
    source_ref="$(published_ref "$repository" "$VERSION-$version_suffix")"
    if tag_exists "$repository" "$alias_tag"; then
      previous_digest="$(tag_digest "$repository" "$alias_tag")"
    fi
    promoted_repositories+=("$repository")
    promoted_aliases+=("$alias_tag")
    promoted_previous_digests+=("$previous_digest")
    promoted_registry_names+=("$REGISTRY_NAME")
    promoted_registry_servers+=("$REGISTRY_SERVER")
    docker buildx imagetools create \
      --tag "$REGISTRY_SERVER/$repository:$alias_tag" \
      "$source_ref"
  }

  rollback_aliases() {
    local status=$?
    local index repository alias_tag previous_digest registry_name registry_server
    trap - EXIT
    if [[ "$promotion_complete" == "true" || "$status" -eq 0 ]]; then
      return
    fi
    set +e
    for ((index=${#promoted_aliases[@]} - 1; index >= 0; index--)); do
      repository="${promoted_repositories[$index]}"
      alias_tag="${promoted_aliases[$index]}"
      previous_digest="${promoted_previous_digests[$index]}"
      registry_name="${promoted_registry_names[$index]}"
      registry_server="${promoted_registry_servers[$index]}"
      if [[ -n "$previous_digest" ]]; then
        echo "Restoring '$registry_server/$repository:$alias_tag' to '$previous_digest'."
        docker buildx imagetools create \
          --tag "$registry_server/$repository:$alias_tag" \
          "$registry_server/$repository@$previous_digest"
      else
        echo "Removing newly created alias '$registry_server/$repository:$alias_tag'."
        az acr repository untag \
          --name "$registry_name" \
          --image "$repository:$alias_tag"
      fi
    done
    exit "$status"
  }

  promoted_repositories=()
  promoted_aliases=()
  promoted_previous_digests=()
  promoted_registry_names=()
  promoted_registry_servers=()
  promotion_complete=false
  trap rollback_aliases EXIT
  use_registry "$CLI_REGISTRY_NAME" "$CLI_REGISTRY_SERVER"
  promote_alias "$CLI_REPOSITORY" azurelinux3.0-lean azurelinux3.0-lean
  promote_alias "$CLI_REPOSITORY" latest-lean azurelinux3.0-lean
  promote_alias "$CLI_REPOSITORY" azurelinux3.0 azurelinux3.0
  promote_alias "$CLI_REPOSITORY" latest azurelinux3.0
  use_registry "$POWERSHELL_REGISTRY_NAME" "$POWERSHELL_REGISTRY_SERVER"
  promote_alias "$POWERSHELL_REPOSITORY" azurelinux-3.0 azurelinux-3.0
  promote_alias "$POWERSHELL_REPOSITORY" latest azurelinux-3.0
  promotion_complete=true
  trap - EXIT
  exit 0
fi

if [[ "$ACTION" != "publish" ]]; then
  echo "ERROR: Unsupported ACTION '$ACTION'. Expected 'publish' or 'promote'."
  exit 1
fi

if [[ -z "$ARTIFACT_ROOT" || ! -d "$ARTIFACT_ROOT" ]]; then
  echo "ERROR: ARTIFACT_ROOT must point to the downloaded Docker artifacts."
  exit 1
fi

if [[ -z "$METADATA_ROOT" ]]; then
  echo "ERROR: METADATA_ROOT is required for publication metadata."
  exit 1
fi
mkdir -p "$METADATA_ROOT"
digest_file="$METADATA_ROOT/image-digests.txt"
: > "$digest_file"

publish_image() {
  local artifact_name="$1"
  local local_repository="$2"
  local repository="$3"
  local local_tag_suffix="$4"
  local tag_suffix="$5"
  local expected_arch="$6"
  local artifact_dir="$ARTIFACT_ROOT/$artifact_name"

  mapfile -t archives < <(find "$artifact_dir" -maxdepth 2 -type f -name '*.tar' | sort)
  if [[ ${#archives[@]} -ne 1 ]]; then
    echo "ERROR: Expected exactly one Docker archive under $artifact_dir, found ${#archives[@]}."
    printf '%s\n' "${archives[@]}"
    exit 1
  fi

  local local_tag="$local_repository:$VERSION-$local_tag_suffix"
  local remote_tag="$REGISTRY_SERVER/$repository:$VERSION-$tag_suffix"
  docker load --input "${archives[0]}"

  local actual_arch
  actual_arch="$(docker image inspect "$local_tag" --format '{{.Architecture}}')"
  if [[ "$actual_arch" != "$expected_arch" ]]; then
    echo "ERROR: Expected '$local_tag' architecture '$expected_arch', found '$actual_arch'."
    exit 1
  fi

  if [[ "$local_repository" == "azclips-powershell" ]]; then
    mapfile -t version_files < <(find "$artifact_dir" -maxdepth 2 -type f -name 'powershell-version.txt' | sort)
    if [[ ${#version_files[@]} -ne 1 ]]; then
      echo "ERROR: Expected exactly one PowerShell version metadata file under '$artifact_dir', found ${#version_files[@]}."
      exit 1
    fi
    local powershell_version
    powershell_version="$(<"${version_files[0]}")"
    if [[ ! "$powershell_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]]; then
      echo "ERROR: Invalid PowerShell Az module version '$powershell_version'."
      exit 1
    fi
    if [[ -f "$METADATA_ROOT/powershell-version.txt" ]]; then
      local recorded_powershell_version
      recorded_powershell_version="$(<"$METADATA_ROOT/powershell-version.txt")"
      if [[ "$recorded_powershell_version" != "$powershell_version" ]]; then
        echo "ERROR: PowerShell image architectures contain different Az module versions: '$recorded_powershell_version' and '$powershell_version'."
        exit 1
      fi
    else
      printf '%s\n' "$powershell_version" > "$METADATA_ROOT/powershell-version.txt"
    fi
  fi

  if tag_exists "$repository" "$VERSION-$tag_suffix"; then
    echo "Versioned image '$remote_tag' already exists. Keeping the existing image."
    record_digest "$repository" "$VERSION-$tag_suffix"
    lock_versioned_tag "$repository" "$VERSION-$tag_suffix"
    return
  fi

  docker tag "$local_tag" "$remote_tag"
  docker push "$remote_tag"
  if ! lock_new_versioned_tag "$repository" "$VERSION-$tag_suffix"; then
    exit 1
  fi
  record_digest "$repository" "$VERSION-$tag_suffix"
}

publish_manifest() {
  local repository="$1"
  local tag_suffix="$2"
  shift 2
  if tag_exists "$repository" "$VERSION-$tag_suffix"; then
    echo "Versioned manifest '$REGISTRY_SERVER/$repository:$VERSION-$tag_suffix' already exists. Keeping the existing manifest."
  else
    create_manifest "$repository" "$VERSION-$tag_suffix" "$@"
    if ! lock_new_versioned_tag "$repository" "$VERSION-$tag_suffix"; then
      exit 1
    fi
    record_digest "$repository" "$VERSION-$tag_suffix"
    return
  fi
  record_digest "$repository" "$VERSION-$tag_suffix"
  lock_versioned_tag "$repository" "$VERSION-$tag_suffix"
}

use_registry "$CLI_REGISTRY_NAME" "$CLI_REGISTRY_SERVER"
publish_image Release_Docker_CLI_AzureLinux3_Lean_Amd64 azclips "$CLI_REPOSITORY" azurelinux3-lean-amd64 azurelinux3.0-lean-amd64 amd64
publish_image Release_Docker_CLI_AzureLinux3_Lean_Arm64 azclips "$CLI_REPOSITORY" azurelinux3-lean-arm64 azurelinux3.0-lean-arm64 arm64
publish_image Release_Docker_CLI_AzureLinux3_Full_Amd64 azclips "$CLI_REPOSITORY" azurelinux3-full-amd64 azurelinux3.0-amd64 amd64
publish_image Release_Docker_CLI_AzureLinux3_Full_Arm64 azclips "$CLI_REPOSITORY" azurelinux3-full-arm64 azurelinux3.0-arm64 arm64

publish_manifest "$CLI_REPOSITORY" azurelinux3.0-lean \
  "$VERSION-azurelinux3.0-lean-amd64" \
  "$VERSION-azurelinux3.0-lean-arm64"
publish_manifest "$CLI_REPOSITORY" azurelinux3.0 \
  "$VERSION-azurelinux3.0-amd64" \
  "$VERSION-azurelinux3.0-arm64"

use_registry "$POWERSHELL_REGISTRY_NAME" "$POWERSHELL_REGISTRY_SERVER"
publish_image Release_Docker_PowerShell_AzureLinux3_Amd64 azclips-powershell "$POWERSHELL_REPOSITORY" azurelinux3-amd64 azurelinux-3.0-amd64 amd64
publish_image Release_Docker_PowerShell_AzureLinux3_Arm64 azclips-powershell "$POWERSHELL_REPOSITORY" azurelinux3-arm64 azurelinux-3.0-arm64 arm64
publish_manifest "$POWERSHELL_REPOSITORY" azurelinux-3.0 \
  "$VERSION-azurelinux-3.0-amd64" \
  "$VERSION-azurelinux-3.0-arm64"

sha256sum "$digest_file" > "$METADATA_ROOT/image-digests.txt.sha256"
