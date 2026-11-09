#!/usr/bin/env bash
# shared setup for the bug-bash-identity scripts. source, don't run directly.
# sets ALIAS and REPO, and sanity-checks the az subscription.

set -euo pipefail

# On Windows Git Bash, MSYS auto-rewrites args that look like absolute paths
# (e.g. "/subscriptions/...") into Windows paths, corrupting az's --scope/--ids
# arguments. Disable that; it's a no-op on Linux/WSL/macOS.
export MSYS_NO_PATHCONV=1

ENV_SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=bug-bash-identity-alias.sh
source "$ENV_SCRIPT_DIR/bug-bash-identity-alias.sh"

if [[ -z "${ALIAS:-}" || "$ALIAS" == "yourname" ]]; then
  echo "ALIAS must be set to your own alias in tests/Live.Tests/Identity/bug-bash-identity-alias.sh (still the default placeholder 'yourname')" >&2
  exit 1
fi

# require a real upstream tracking branch with the same name - this is also exactly
# what "gh workflow run --ref <branch>" needs to find the branch remotely, so
# enforcing it here catches a missing/renamed push before anything else runs.
BRANCH="$(git symbolic-ref -q --short HEAD || true)"
if [[ -z "$BRANCH" ]]; then
  echo "You're in a detached HEAD state. Check out a branch first." >&2
  exit 1
fi

REMOTE_NAME="$(git config --get "branch.$BRANCH.remote" || true)"
tracked_merge_ref="$(git config --get "branch.$BRANCH.merge" || true)"
tracked_branch="${tracked_merge_ref#refs/heads/}"

if [[ -z "$REMOTE_NAME" || -z "$tracked_branch" ]]; then
  echo "Branch '$BRANCH' has no upstream tracking branch. Push it first with" \
       "'git push -u <your-fork-remote> $BRANCH' so this script knows which remote/repo" \
       "to use." >&2
  exit 1
fi

if [[ "$tracked_branch" != "$BRANCH" ]]; then
  echo "Branch '$BRANCH' tracks '$REMOTE_NAME/$tracked_branch' - a different name." \
       "Push/track a remote branch named '$BRANCH' so 'gh workflow run --ref $BRANCH'" \
       "can find it." >&2
  exit 1
fi

origin_url="$(git config --get "remote.$REMOTE_NAME.url" || true)"
if [[ -z "$origin_url" ]]; then
  echo "Could not read the '$REMOTE_NAME' git remote URL. Run this from inside the repo." >&2
  exit 1
fi

# normalize git@github.com:owner/repo.git and https URLs to owner/repo
REPO="$origin_url"
REPO="${REPO#git@github.com:}"
REPO="${REPO#https://github.com/}"
REPO="${REPO%.git}"

repo_owner="${REPO%%/*}"
repo_name="${REPO#*/}"

# GitHub owners are case-insensitive, so compare lowercased
repo_owner_lower="$(tr '[:upper:]' '[:lower:]' <<< "$repo_owner")"
if [[ "$repo_owner_lower" == "azure" ]]; then
  echo "'$REMOTE_NAME' is '$REPO' - that's upstream. Run this from your own fork." >&2
  exit 1
fi

repo_name_lower="$(tr '[:upper:]' '[:lower:]' <<< "$repo_name")"
if [[ "$repo_name_lower" != *cirrus* && "$repo_name_lower" != *azclips* ]]; then
  echo "'$REMOTE_NAME' repo '$REPO' doesn't look like a cirrus/azclips fork." >&2
  exit 1
fi

EXPECTED_SUBSCRIPTION_NAME="Azure CLI/PWSH"
active_subscription_name="$(az account show --query name -o tsv 2>/dev/null || true)"
if [[ "$active_subscription_name" != "$EXPECTED_SUBSCRIPTION_NAME" ]]; then
  echo "Active az subscription is '$active_subscription_name', expected" \
       "'$EXPECTED_SUBSCRIPTION_NAME'. Run 'az account set --subscription ...' first." >&2
  exit 1
fi

echo "Detected fork: $REPO | alias: $ALIAS | subscription: $active_subscription_name"
