#!/usr/bin/env bash
#
# tf-common.sh — shared helpers for the stage 0 / stage 1 Terraform wrappers.
#
# Sourced by:
#   terraform/bootstrap/setup-github-identity.sh      (stage 0, local only)
#   terraform/linux-vm/provision-infrastructure.sh    (stage 1, local and CI)
#
# Not executable on its own.

# The environment is identified by subscription *name*, not ID, so each
# developer can run against their own MSDN subscription without editing this
# file. Everything else (provider config, state account name) is derived from
# whichever subscription is active — see require_az_context.
#
# Caveat: this name is the MSDN default, so it is deliberately not unique. It
# guards against "wrong subscription selected", not against a determined mistake.
EXPECTED_SUBSCRIPTION_NAME="${EXPECTED_SUBSCRIPTION_NAME:-Visual Studio Enterprise Subscription}"

# The GitHub repository must be the developer's own fork. Publishing the OIDC
# identity to the shared upstream would hand every contributor one developer's
# Azure credentials, so those owners are rejected outright.
# Both names are accepted: the upstream is Azure/azclips, so a default fork is
# named <user>/azclips, while a renamed one may be <user>/cirrus.
EXPECTED_REPO_NAMES="cirrus azclips"
UPSTREAM_OWNERS="Azure"

# Must stay in sync with the `alias` validation block in
# terraform/bootstrap/variables.tf and terraform/linux-vm/variables.tf.
#
# 3-14 characters. The upper bound is set by the state storage account, whose
# name is capped at 24: alias (hyphens stripped, <=14) + "tf" + 8 hex digits of
# the subscription ID = 24 exactly.
ALIAS_REGEX='^[a-z][a-z0-9-]{1,12}[a-z0-9]$'

# Resolve the developer alias.
#
# Backend blocks are evaluated before Terraform variables exist, so the state
# blob key cannot interpolate var.alias — the wrapper script has to know the
# alias too. Precedence:
#   1. TF_VAR_alias  (how CI supplies it, from the ALIAS GitHub variable)
#   2. terraform.tfvars in the module directory (how a developer supplies it)
#
# Echoes the alias on success; exits non-zero with a diagnostic otherwise.
resolve_alias() {
  local module_dir="$1"
  local tfvars="$module_dir/terraform.tfvars"
  local alias_value="${TF_VAR_alias:-}"
  local source="TF_VAR_alias"

  if [[ -z "$alias_value" && -f "$tfvars" ]]; then
    source="$tfvars"
    # Take the last assignment, tolerate whitespace, strip quotes and comments.
    alias_value="$(sed -n 's/^[[:space:]]*alias[[:space:]]*=[[:space:]]*"\([^"]*\)".*/\1/p' "$tfvars" | tail -n 1)"
  fi

  if [[ -z "$alias_value" ]]; then
    echo "!! No 'alias' configured." >&2
    echo "   Set it in $tfvars (copy terraform.tfvars.example), or export TF_VAR_alias." >&2
    echo "   The alias prefixes your resources and selects your Terraform state." >&2
    return 1
  fi

  if [[ ! "$alias_value" =~ $ALIAS_REGEX ]]; then
    echo "!! Invalid alias '$alias_value' (from $source)." >&2
    echo "   Must be 3-14 characters, lowercase, start with a letter, and contain" >&2
    echo "   only a-z, 0-9 and '-'." >&2
    return 1
  fi

  printf '%s' "$alias_value"
}

# Guard against an alias change silently orphaning infrastructure.
#
# Changing the alias points `terraform init` at a DIFFERENT state blob, so
# Terraform sees empty state and creates a second full set of resources while the
# originals keep running (and billing) untracked. The marker file records the
# alias that last applied successfully in this working copy.
check_alias_drift() {
  local module_dir="$1"
  local alias_value="$2"
  local marker="$module_dir/.alias"

  [[ -f "$marker" ]] || return 0

  local previous
  previous="$(tr -d '[:space:]' < "$marker")"
  [[ -z "$previous" || "$previous" == "$alias_value" ]] && return 0

  echo "!! Alias changed: '$previous' -> '$alias_value'." >&2
  echo "   Each alias has its own Terraform state, so continuing would leave the" >&2
  echo "   '$previous' resources running and untracked." >&2
  echo "   Either destroy them first (re-run this script with TF_VAR_alias=$previous" >&2
  echo "   and 'terraform destroy'), or, if they are already gone, remove the marker:" >&2
  echo "     rm $marker" >&2
  return 1
}

record_alias() {
  printf '%s\n' "$2" > "$1/.alias"
}

# Ensure the Terraform state storage account and container exist, then export
# ARM_ACCESS_KEY for the azurerm backend.
#
# The account holds ONLY Terraform state and is shared by every developer — it is
# unrelated to the per-developer workload resource groups, which stage 1 creates
# and destroys. Isolation between developers comes from the state blob key, not
# from separate accounts.
#
# The account is located by name across the whole subscription rather than by a
# hardcoded resource group, so it keeps working if the group was renamed or the
# account was created elsewhere. Storage account names are globally unique, so
# blindly creating one in the "wrong" group would fail with a name-taken error
# instead of finding the existing state.
ensure_state_backend() {
  local storage_account="$1" container="$2" fallback_group="$3" location="${4:-australiaeast}"
  local resource_group

  # `|| true` is required: the callers run with `set -o pipefail`, so a missing
  # account (az exits non-zero) would otherwise abort the script instead of
  # falling through to creation.
  resource_group="$(az storage account show \
    --name "$storage_account" \
    --query resourceGroup -o tsv 2>/dev/null | tr -d '\r' || true)"

  if [[ -z "$resource_group" ]]; then
    resource_group="$fallback_group"
    echo "==> Terraform state account $storage_account not found; creating it in $resource_group"

    if ! az group show --name "$resource_group" -o none 2>/dev/null; then
      echo "==> Creating resource group $resource_group ($location)"
      az group create --name "$resource_group" --location "$location" -o none
    fi

    az storage account create \
      --name "$storage_account" \
      --resource-group "$resource_group" \
      --location "$(az group show --name "$resource_group" --query location -o tsv | tr -d '\r')" \
      --sku Standard_LRS \
      --kind StorageV2 \
      --min-tls-version TLS1_2 \
      --allow-blob-public-access false \
      -o none
  else
    echo "==> Terraform state account $storage_account found in $resource_group"
  fi

  export ARM_ACCESS_KEY
  ARM_ACCESS_KEY="$(az storage account keys list \
    --account-name "$storage_account" \
    --resource-group "$resource_group" \
    --query '[0].value' -o tsv | tr -d '\r')"

  az storage container create \
    --name "$container" \
    --account-name "$storage_account" \
    --account-key "$ARM_ACCESS_KEY" \
    -o none
}

# Assert the Azure CLI is installed, signed in, and pointed at the expected
# tenant and subscription. Call this FIRST in every entry point: everything below
# it either costs minutes (building a bundle) or would fail later with a much
# less obvious error.
#
# Defaults to the pinned EXPECTED_* values; pass overrides for other environments.
# Verify the CLI is signed in and pointed at a subscription with the expected
# name, then export the resolved IDs for callers:
#   AZ_TENANT_ID, AZ_SUBSCRIPTION_ID, AZ_SUBSCRIPTION_NAME
# Callers feed those to Terraform, so the active subscription is the single
# source of truth and nothing is hardcoded to one developer's environment.
require_az_context() {
  local expected_name="${1:-$EXPECTED_SUBSCRIPTION_NAME}"
  local account
  local -a _fields

  if ! command -v az >/dev/null 2>&1; then
    echo "!! The Azure CLI (az) is not installed, or is not on PATH." >&2
    return 1
  fi

  # One az call, not three: each invocation costs ~1s under WSL interop.
  if ! account="$(az account show --query "[tenantId,id,name]" -o tsv 2>/dev/null | tr -d '\r')" \
    || [[ -z "$account" ]]; then
    echo "!! Not signed in to Azure. Run:" >&2
    echo "     az login" >&2
    echo "     az account set --subscription \"$expected_name\"" >&2
    return 1
  fi

  # `-o tsv` returns a list query one value per line, not tab-separated.
  mapfile -t _fields <<<"$account"
  AZ_TENANT_ID="${_fields[0]:-}"
  AZ_SUBSCRIPTION_ID="${_fields[1]:-}"
  AZ_SUBSCRIPTION_NAME="${_fields[2]:-}"

  if [[ "$AZ_SUBSCRIPTION_NAME" != "$expected_name" ]]; then
    echo "!! Azure CLI is pointed at the wrong subscription." >&2
    echo "   Expected: $expected_name" >&2
    echo "   Current:  $AZ_SUBSCRIPTION_NAME ($AZ_SUBSCRIPTION_ID)" >&2
    echo "   Fix with:" >&2
    echo "     az account set --subscription \"$expected_name\"" >&2
    return 1
  fi

  if [[ -z "$AZ_TENANT_ID" || -z "$AZ_SUBSCRIPTION_ID" ]]; then
    echo "!! Could not resolve tenant/subscription ID from 'az account show'." >&2
    return 1
  fi

  export AZ_TENANT_ID AZ_SUBSCRIPTION_ID AZ_SUBSCRIPTION_NAME
}

# Resolve the GitHub repository the OIDC credential should trust, and refuse to
# touch the shared upstream.
#
# Deliberately reads `origin` rather than `gh repo view`: when a fork has an
# `upstream` remote, gh resolves to the *parent* repo, which would publish this
# developer's Azure credentials to the shared upstream instead of their fork.
#
# Exports GH_REPO ("owner/repo") and GH_OWNER.
require_gh_repo() {
  local url owner repo

  if ! command -v gh >/dev/null 2>&1; then
    echo "!! The GitHub CLI (gh) is not installed, or is not on PATH." >&2
    return 1
  fi

  if ! gh auth status >/dev/null 2>&1; then
    echo "!! Not signed in to GitHub. Run: gh auth login" >&2
    return 1
  fi

  if ! url="$(git remote get-url origin 2>/dev/null)" || [[ -z "$url" ]]; then
    echo "!! No 'origin' git remote found; cannot tell which repository to configure." >&2
    return 1
  fi

  # Accept both git@github.com:owner/repo.git and https://github.com/owner/repo(.git)
  if [[ "$url" =~ ^(git@github\.com:|https://github\.com/)([^/]+)/(.+?)(\.git)?$ ]]; then
    owner="${BASH_REMATCH[2]}"
    repo="${BASH_REMATCH[3]%.git}"
  else
    echo "!! Could not parse a GitHub owner/repo out of origin: $url" >&2
    return 1
  fi

  GH_OWNER="$owner"
  GH_REPO="${owner}/${repo}"

  local name_ok=0 candidate
  for candidate in $EXPECTED_REPO_NAMES; do
    [[ "${repo,,}" == "${candidate,,}" ]] && name_ok=1 && break
  done
  if [[ "$name_ok" -eq 0 ]]; then
    echo "!! 'origin' does not look like your fork of this project." >&2
    echo "   origin: $GH_REPO" >&2
    echo "   Expected a repository named one of: $EXPECTED_REPO_NAMES" >&2
    return 1
  fi

  # Never write to the shared upstream: the workflow identity is per developer.
  local blocked
  for blocked in $UPSTREAM_OWNERS; do
    if [[ "${owner,,}" == "${blocked,,}" ]]; then
      echo "!! Refusing to configure the shared upstream repository '$GH_REPO'." >&2
      echo "   This creates a per-developer identity and must point at your own fork." >&2
      echo "   Set 'origin' to your fork (upstream stays as a separate remote)." >&2
      return 1
    fi
  done

  local login
  login="$(gh api user --jq .login 2>/dev/null)" || login=""
  if [[ -n "$login" && "${login,,}" != "${owner,,}" ]]; then
    echo "!! Signed in to GitHub as '$login', but origin belongs to '$owner'." >&2
    echo "   Sign in as '$owner' (gh auth login), or point origin at your own fork." >&2
    return 1
  fi

  export GH_REPO GH_OWNER
}

# Name of the state storage account: alias-prefixed so it is recognisable, with
# 8 hex digits of the subscription ID appended because the name must be globally
# unique. Hyphens are stripped — storage account names allow lowercase
# alphanumerics only. Length is bounded to 24 by ALIAS_REGEX; see above.
state_account_name() {
  local alias="${1:?state_account_name requires an alias}"
  echo "${alias//-/}tf${AZ_SUBSCRIPTION_ID:0:8}"
}

# terraform plan + apply, skipping the apply when nothing has changed.
# -detailed-exitcode: 0 = no changes, 1 = error, 2 = changes pending.
plan_and_apply() {
  local plan_file="${1:-provision.tfplan}"
  local rc=0

  echo "==> terraform plan (checking for pending changes)"
  terraform plan -input=false -detailed-exitcode -out="$plan_file" || rc=$?

  case "$rc" in
    0)
      echo "==> No changes — infrastructure is up to date. Skipping apply."
      rm -f "$plan_file"
      ;;
    2)
      echo "==> Applying pending changes (auto-approve)"
      terraform apply -input=false -auto-approve "$plan_file"
      rm -f "$plan_file"
      ;;
    *)
      echo "!! terraform plan failed (exit $rc)." >&2
      rm -f "$plan_file"
      return "$rc"
      ;;
  esac
}
