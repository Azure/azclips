#!/usr/bin/env bash
set -euo pipefail

# Exercise the public download path with the native pipeline archive, not a live release.
repo_root="$1"
archive_path="$2"
archive_name="$(basename "$archive_path")"
fixture_root="$(mktemp -d)"
trap 'rm -rf "$fixture_root"' EXIT
mkdir -p "$fixture_root/bin" "$fixture_root/tmp"
export FIXTURE_ROOT="$fixture_root" FIXTURE_ARCHIVE="$archive_path"
export FIXTURE_URL="https://github.com/${AZCLI_REPO:-Azure/azclips}/releases/download/v-fixture.timestamp/$archive_name"
export TMPDIR="$fixture_root/tmp"
unset GH_TOKEN GITHUB_TOKEN GH_ENTERPRISE_TOKEN GITHUB_ENTERPRISE_TOKEN

# Limit PATH to standard utilities so jq, gh, and Python are genuinely unavailable.
for utility in bash basename cat chmod cksum cp dirname grep gzip ls mkdir mktemp mv rm sed tar touch tr uname; do
  ln -s "$(command -v "$utility")" "$fixture_root/bin/$utility"
done
if command -v ldd >/dev/null; then
  ln -s "$(command -v ldd)" "$fixture_root/bin/ldd"
fi

if [[ "$archive_name" == azcli-lean-* ]]; then
  other_profile="${archive_name/azcli-lean-/azcli-}"
else
  other_profile="${archive_name/azcli-/azcli-lean-}"
fi
printf '{"assets":[{"browser_download_url" :\n "%s"}, {"browser_download_url":"%s"}, {"browser_download_url":"%s"}, {"other_url":"%s"}]}\n' \
  "$FIXTURE_URL" "${FIXTURE_URL%/*}/$other_profile" \
  "${FIXTURE_URL/github.com/github.com.evil.invalid}" "$FIXTURE_URL" > "$fixture_root/release.json"
cat > "$fixture_root/bin/curl" <<'STUB'
#!/usr/bin/env bash
set -euo pipefail
url=""
destination=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    -o) destination="$2"; shift 2 ;;
    https://*) url="$1"; shift ;;
    *) shift ;;
  esac
done
case "$url" in
  "https://api.github.com/repos/${AZCLI_REPO:-Azure/azclips}/releases/latest")
    case "${FIXTURE_CASE:-valid}" in
      metadata-failure) echo "HTTP 403: fixture rate limit" >&2; exit 22 ;;
      missing) printf '{"assets":[]}' > "$destination" ;;
      duplicate) printf '{"assets":[{"browser_download_url":"%s"},{"browser_download_url":"%s"}]}' "$FIXTURE_URL" "$FIXTURE_URL" > "$destination" ;;
      malformed) printf '{invalid' > "$destination" ;;
      query) printf '{"browser_download_url":"%s?download=1"}' "$FIXTURE_URL" > "$destination" ;;
      extra-path) printf '{"browser_download_url":"%s/extra"}' "$FIXTURE_URL" > "$destination" ;;
      http) printf '{"browser_download_url":"%s"}' "${FIXTURE_URL/https:/http:}" > "$destination" ;;
      wrong-repo) printf '{"browser_download_url":"%s"}' "${FIXTURE_URL/github.com\//github.com/other-}" > "$destination" ;;
      missing-quote) printf '{"browser_download_url":"%s' "$FIXTURE_URL" > "$destination" ;;
      other-field) printf '{"body":"%s"}' "$FIXTURE_URL" > "$destination" ;;
      escaped-body) printf '{"body":"\\"browser_download_url\\":\\"%s\\""}' "$FIXTURE_URL" > "$destination" ;;
      unsafe-filename) printf '{"browser_download_url":"%s"}' "${FIXTURE_URL/.tar.gz/%20.tar.gz}" > "$destination" ;;
      wrong-platform) printf '{"browser_download_url":"%s"}' "${FIXTURE_URL/.tar.gz/-other.tar.gz}" > "$destination" ;;
      compact) printf '{"assets":[{"browser_download_url":"%s"}]}' "$FIXTURE_URL" > "$destination" ;;
      *) cp "$FIXTURE_ROOT/release.json" "$destination" ;;
    esac
    ;;
  "$FIXTURE_URL")
    if [[ "${FIXTURE_CASE:-valid}" == download-failure ]]; then
      echo "HTTP 503: fixture download failed" >&2
      exit 22
    fi
    cp "$FIXTURE_ARCHIVE" "$destination"
    touch "$FIXTURE_ROOT/downloaded"
    ;;
  *) echo "Unexpected download URL: $url" >&2; exit 1 ;;
esac
STUB
chmod +x "$fixture_root/bin/curl"
export PATH="$fixture_root/bin"
for unavailable in jq gh python python3; do
  if command -v "$unavailable" >/dev/null; then
    echo "FAIL: $unavailable must be unavailable during public installer validation." >&2
    exit 1
  fi
done

before="$(cksum "$AZCLI_INSTALL_ROOT/az")"
for script in azcli-install.sh azcli-uninstall.sh; do
  help="$(bash -s -- --help < "$repo_root/install/$script")"
  grep -q 'Usage:' <<<"$help" || { echo "FAIL: Piped $script help is missing." >&2; exit 1; }
  sed '$d' "$repo_root/install/$script" | bash
  [[ "$(cksum "$AZCLI_INSTALL_ROOT/az")" == "$before" ]] ||
    { echo "FAIL: Truncated $script changed the installation." >&2; exit 1; }
done

for scenario in missing duplicate malformed query extra-path http wrong-repo missing-quote other-field escaped-body unsafe-filename wrong-platform metadata-failure download-failure; do
  export FIXTURE_CASE="$scenario"
  case "$scenario" in
    metadata-failure) expected='anonymous API rate limit' ;;
    download-failure) expected='Could not download' ;;
    *) expected='Expected exactly one' ;;
  esac
  if output="$(bash < "$repo_root/install/azcli-install.sh" 2>&1)"; then
    echo "FAIL: $scenario unexpectedly succeeded." >&2
    exit 1
  fi
  if ! grep -Fq "$expected" <<<"$output"; then
    printf 'FAIL: %s did not report the expected error:\n%s\n' "$scenario" "$output" >&2
    exit 1
  fi
  [[ "$(cksum "$AZCLI_INSTALL_ROOT/az")" == "$before" ]] ||
    { echo "FAIL: $scenario changed the existing installation." >&2; exit 1; }
  [[ -z "$(ls -A "$TMPDIR")" ]] ||
    { echo "FAIL: $scenario left temporary download files." >&2; exit 1; }
done

for scenario in valid compact; do
  export FIXTURE_CASE="$scenario"
  bash < "$repo_root/install/azcli-install.sh"
done
[[ -f "$fixture_root/downloaded" ]] ||
  { echo "FAIL: Public installation did not use only the fixture download." >&2; exit 1; }
echo "PASS: Public download selection, failure handling, piped help, and installation using the pipeline archive without jq, gh, or Python."
