#!/usr/bin/env bash
#
# tf-common.sh — shared helpers for the stage 1 Terraform wrapper.
#
# Sourced by:
#   tests/Live/Terraform/linux-vm/provision-infrastructure.sh    (stage 1, local and CI)
#
# Not executable on its own.

# The environment is identified by subscription *name*, not ID, so each
# developer can run against their own subscription without editing this file.
# Everything else (provider config, state account name) is derived from
# whichever subscription is active — see require_az_context.
#
# Name, not ID, on purpose: these tenants are rotated periodically, and the
# replacement contains a subscription with the same name but a NEW ID. Matching
# on name means a developer only has to sign in to the new tenant; matching on
# ID would need a code change every rotation. Everything downstream keys off the
# resolved ID, so the new state account and resources are created automatically.
#
# Caveat: names are not unique across tenants, and during a rotation both the
# old and new subscription are visible at once. This guards against "wrong
# subscription selected", not against a determined mistake.
EXPECTED_SUBSCRIPTION_NAME="${EXPECTED_SUBSCRIPTION_NAME:-Azure CLI/PWSH}"

# Terraform state location. Shared by every entry point so the wrappers and the
# read-only consumers (tests/Live/Tool) cannot disagree about where state lives.
BACKEND_CONTAINER="tfstate"
BACKEND_LOCATION="australiaeast"

# Directory holding this file (terraform/), used to locate the shared tfvars.
TF_COMMON_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Must stay in sync with the `alias` validation block in
# tests/Live/Terraform/linux-vm/variables.tf.
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
#   2. terraform.tfvars in the module directory (per-module override)
#   3. tests/Live/Terraform/terraform.tfvars (shared by both stages — the normal case)
#
# Echoes the alias on success; exits non-zero with a diagnostic otherwise.
resolve_alias() {
  local module_dir="$1"
  local shared_tfvars="${TF_COMMON_DIR}/terraform.tfvars"
  local module_tfvars="$module_dir/terraform.tfvars"
  local alias_value="${TF_VAR_alias:-}"
  local source="TF_VAR_alias"
  local file

  # Precedence: env (how CI supplies it) > module-local tfvars (per-module
  # override) > shared tests/Live/Terraform/terraform.tfvars (the normal case: configure
  # the alias once for both stages).
  if [[ -z "$alias_value" ]]; then
    for file in "$module_tfvars" "$shared_tfvars"; do
      [[ -f "$file" ]] || continue
      # Take the last assignment, tolerate whitespace, strip quotes and comments.
      alias_value="$(sed -n 's/^[[:space:]]*alias[[:space:]]*=[[:space:]]*"\([^"]*\)".*/\1/p' "$file" | tail -n 1)"
      if [[ -n "$alias_value" ]]; then
        source="$file"
        break
      fi
    done
  fi

  if [[ -z "$alias_value" ]]; then
    echo "!! No 'alias' configured." >&2
    echo "   Set it in $shared_tfvars (copy tests/Live/Terraform/terraform.tfvars.example)," >&2
    echo "   or export TF_VAR_alias." >&2
    echo "   The alias prefixes your resources and selects your Terraform state." >&2
    return 1
  fi

  if [[ ! "$alias_value" =~ $ALIAS_REGEX ]]; then
    echo "!! Invalid alias '$alias_value' (from $source)." >&2
    echo "   Must be 3-14 characters, lowercase, start with a letter, and contain" >&2
    echo "   only a-z, 0-9 and '-'. It cannot end with '-'." >&2
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
# Print a switch command that actually works.
#
# `az account set --subscription <name>` fails with "has more than one match"
# whenever the expiring tenant and its replacement are both signed in, which is
# exactly the window in which someone needs this advice. So resolve the name to
# concrete IDs and print those instead, ignoring subscriptions that are no
# longer Enabled — the expired tenant's copy is never the intended target.
suggest_subscription_switch() {
  local expected_name="${1:?suggest_subscription_switch requires a name}"
  local -a candidates=()

  # `|| true`: callers run under `set -o pipefail`, and no match must fall
  # through to the login hint rather than abort the script.
  mapfile -t candidates < <(az account list --all \
    --query "[?name=='${expected_name//\'/\'\'}' && state=='Enabled'].[id,tenantId]" \
    -o tsv 2>/dev/null | tr -d '\r' || true)

  echo "   Fix with:" >&2

  if [[ ${#candidates[@]} -eq 0 ]]; then
    echo "     az login" >&2
    echo "   No enabled subscription named '$expected_name' is visible. If the tenant was" >&2
    echo "   just rotated, sign in to the new one:" >&2
    echo "     az login --tenant <new-tenant-id>" >&2
    return 0
  fi

  local entry id tenant
  for entry in "${candidates[@]}"; do
    IFS=$'\t' read -r id tenant <<<"$entry"
    echo "     az account set --subscription $id   # tenant $tenant" >&2
  done

  # Names are not unique across tenants, and during a rotation two identically
  # named subscriptions are visible at once. Only the caller knows which tenant
  # is current, so ask rather than guess.
  if [[ ${#candidates[@]} -gt 1 ]]; then
    echo "   ${#candidates[@]} enabled subscriptions share this name (a tenant rotation in" >&2
    echo "   progress leaves both visible). Choose the NEWER tenant." >&2
  fi
}

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
  if ! account="$(az account show --query "[tenantId,id,name,state]" -o tsv 2>/dev/null | tr -d '\r')" \
    || [[ -z "$account" ]]; then
    echo "!! Not signed in to Azure. Run:" >&2
    echo "     az login" >&2
    echo "   then select the '$expected_name' subscription." >&2
    return 1
  fi

  # `-o tsv` returns a list query one value per line, not tab-separated.
  mapfile -t _fields <<<"$account"
  AZ_TENANT_ID="${_fields[0]:-}"
  AZ_SUBSCRIPTION_ID="${_fields[1]:-}"
  AZ_SUBSCRIPTION_NAME="${_fields[2]:-}"
  AZ_SUBSCRIPTION_STATE="${_fields[3]:-}"

  if [[ "$AZ_SUBSCRIPTION_NAME" != "$expected_name" ]]; then
    echo "!! Azure CLI is pointed at the wrong subscription." >&2
    echo "   Expected: $expected_name" >&2
    echo "   Current:  $AZ_SUBSCRIPTION_NAME ($AZ_SUBSCRIPTION_ID)" >&2
    suggest_subscription_switch "$expected_name"
    return 1
  fi

  # The old tenant is disabled before it disappears, and its subscription stays
  # selected and still matches by name. Every later call would fail with an
  # opaque authorization error, so say plainly that a new tenant must be used.
  if [[ -n "$AZ_SUBSCRIPTION_STATE" && "$AZ_SUBSCRIPTION_STATE" != "Enabled" ]]; then
    echo "!! Subscription '$AZ_SUBSCRIPTION_NAME' ($AZ_SUBSCRIPTION_ID) is $AZ_SUBSCRIPTION_STATE, not Enabled." >&2
    echo "   These tenants are rotated periodically; the old one is disabled and replaced" >&2
    echo "   by a new tenant containing a subscription of the same name." >&2
    suggest_subscription_switch "$expected_name"
    return 1
  fi

  if [[ -z "$AZ_TENANT_ID" || -z "$AZ_SUBSCRIPTION_ID" ]]; then
    echo "!! Could not resolve tenant/subscription ID from 'az account show'." >&2
    return 1
  fi

  export AZ_TENANT_ID AZ_SUBSCRIPTION_ID AZ_SUBSCRIPTION_NAME AZ_SUBSCRIPTION_STATE
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

# Resource group used only if the state account has to be created.
backend_resource_group() {
  echo "${1:?backend_resource_group requires an alias}-azclips-live-test-mi-tfstate-rg"
}

# Point a module at this developer's remote state without applying anything.
# `terraform output` cannot read remote state until the backend is initialised
# and ARM_ACCESS_KEY is set, so any script that only *reads* outputs must call
# this first. Quiet on success.
init_module_state() {
  local module_dir="${1:?}" module_name="${2:?}" alias account
  alias="$(resolve_alias "$module_dir")" || return 1
  export TF_VAR_alias="$alias"
  account="$(state_account_name "$alias")"

  ensure_state_backend "$account" "$BACKEND_CONTAINER" \
    "$(backend_resource_group "$alias")" "$BACKEND_LOCATION" >/dev/null

  terraform -chdir="$module_dir" init -input=false -reconfigure \
    -backend-config="storage_account_name=${account}" \
    -backend-config="key=${alias}-${module_name}.tfstate" >/dev/null
}
