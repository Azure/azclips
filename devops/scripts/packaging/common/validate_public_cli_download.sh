#!/usr/bin/env bash
set -euo pipefail

# Exercise the public download path with the native pipeline archive, not a live release.
repo_root="$1"
archive_path="$2"
extensions_dir="$3"
archive_name="$(basename "$archive_path")"
fixture_root="$(mktemp -d)"
trap 'rm -rf "$fixture_root"' EXIT
mkdir -p "$fixture_root/bin" "$fixture_root/tmp"
export FIXTURE_ROOT="$fixture_root" FIXTURE_ARCHIVE="$archive_path"
export FIXTURE_URL="https://github.com/${AZCLI_REPO:-Azure/azclips}/releases/download/v-fixture.timestamp/$archive_name"
export TMPDIR="$fixture_root/tmp"
unset GH_TOKEN GITHUB_TOKEN GH_ENTERPRISE_TOKEN GITHUB_ENTERPRISE_TOKEN

# Limit PATH to the installer's declared system utilities.
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
write_status=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    -o) destination="$2"; shift 2 ;;
    --write-out|-w) write_status=true; shift 2 ;;
    https://*) url="$1"; shift ;;
    *) shift ;;
  esac
done
case "$url" in
  "https://api.github.com/repos/${AZCLI_REPO:-Azure/azclips}/releases/latest"|"https://api.github.com/repos/Azure/azclips/releases/tags/v-fixture.timestamp")
    printf '%s\n' "$url" > "$FIXTURE_ROOT/metadata-url"
    case "${FIXTURE_CASE:-valid}" in
      metadata-failure|rate-limit|not-found|not-found-latest)
        case "$FIXTURE_CASE" in
          metadata-failure) status=403 ;;
          rate-limit) status=429 ;;
          not-found|not-found-latest) status=404 ;;
        esac
        if [[ "$write_status" == true ]]; then printf '%s' "$status"; fi
        echo "HTTP $status: fixture metadata failure" >&2
        exit 22 ;;
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
      printf 'partial download' > "$destination"
      echo "HTTP 503: fixture download failed" >&2
      exit 22
    fi
    if [[ "${FIXTURE_CASE:-valid}" == missing-download ]]; then exit 0; fi
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
    metadata-failure) expected='API rate limit' ;;
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
echo "PASS: Download selection, failure handling, piped help, and installation using the pipeline archive."

export FIXTURE_ARCHIVE="$fixture_root/azcli-extensions-0.0.0.tar.gz"
export FIXTURE_URL="https://github.com/Azure/azclips/releases/download/v-fixture.timestamp/azcli-extensions-0.0.0.tar.gz"
tar -czf "$FIXTURE_ARCHIVE" -C "$extensions_dir" .
printf '{"assets":[{"browser_download_url" :\n "%s"}, {"other_url":"%s"}]}' \
  "$FIXTURE_URL" "$FIXTURE_URL" > "$fixture_root/release.json"
mkdir "$fixture_root/extensions-download"
cd "$fixture_root/extensions-download"
printf 'existing bundle sentinel\n' > azcli-extensions.tar.gz
before="$(cksum azcli-extensions.tar.gz)"
for scenario in missing duplicate malformed query extra-path http wrong-repo missing-quote other-field escaped-body unsafe-filename metadata-failure rate-limit not-found not-found-latest download-failure missing-download; do
  export FIXTURE_CASE="$scenario"
  case "$scenario" in
    metadata-failure|rate-limit|not-found-latest) expected='API rate limit' ;;
    not-found) expected="Release tag 'v-fixture.timestamp' was not found" ;;
    download-failure) expected='Could not download' ;;
    missing-download) expected='Download did not produce' ;;
    *) expected='Expected exactly one' ;;
  esac
  if output="$(
    if [[ "$scenario" == not-found || "$scenario" == metadata-failure || "$scenario" == rate-limit ]]; then
      bash -s -- --release v-fixture.timestamp < "$repo_root/install/azcli-extensions-download.sh" 2>&1
    else
      bash < "$repo_root/install/azcli-extensions-download.sh" 2>&1
    fi
  )"; then
    echo "FAIL: Extensions download $scenario unexpectedly succeeded." >&2; exit 1
  fi
  grep -Fq "$expected" <<<"$output" ||
    { printf 'FAIL: Extensions download %s: %s\n' "$scenario" "$output" >&2; exit 1; }
  [[ "$(cksum azcli-extensions.tar.gz)" == "$before" ]] ||
    { echo "FAIL: Extensions download $scenario changed the existing bundle." >&2; exit 1; }
  if compgen -G '.azcli-extensions-download.*' >/dev/null; then
    echo "FAIL: Extensions download $scenario left staging files." >&2; exit 1
  fi
done
for scenario in valid compact; do
  export FIXTURE_CASE="$scenario"
  bash < "$repo_root/install/azcli-extensions-download.sh"
  [[ "$(cksum < azcli-extensions.tar.gz)" == "$(cksum < "$FIXTURE_ARCHIVE")" ]] ||
    { echo "FAIL: Downloaded extensions bundle differs from the pipeline packages." >&2; exit 1; }
done
bash -s -- --release v-fixture.timestamp < "$repo_root/install/azcli-extensions-download.sh"
grep -q '/tags/v-fixture.timestamp$' "$FIXTURE_ROOT/metadata-url" ||
  { echo "FAIL: Extensions download ignored the pinned release." >&2; exit 1; }
original_url="$FIXTURE_URL"
export FIXTURE_URL="${FIXTURE_URL/v-fixture.timestamp/v-other}"
if output="$(bash -s -- --release v-fixture.timestamp < "$repo_root/install/azcli-extensions-download.sh" 2>&1)"; then
  echo "FAIL: Extensions download accepted a different release." >&2; exit 1
fi
grep -q 'Expected exactly one' <<<"$output"
export FIXTURE_URL="$original_url"
if bash -s -- --release '../invalid' < "$repo_root/install/azcli-extensions-download.sh" > "$fixture_root/invalid-tag.log" 2>&1; then
  echo "FAIL: Invalid release tag was accepted." >&2; exit 1
fi
grep -q 'Invalid release tag' "$fixture_root/invalid-tag.log"
bash -s -- --help < "$repo_root/install/azcli-extensions-download.sh" | grep -q 'Usage:'
sed '$d' "$repo_root/install/azcli-extensions-download.sh" | bash
shopt -s dotglob nullglob
downloaded_files=(*)
[[ -f azcli-extensions.tar.gz && "${#downloaded_files[@]}" -eq 1 ]] ||
  { echo "FAIL: Download helper extracted files or left staging files." >&2; exit 1; }
mv azcli-extensions.tar.gz "$fixture_root/downloaded-bundle.tar.gz"
mkdir azcli-extensions.tar.gz
if output="$(bash < "$repo_root/install/azcli-extensions-download.sh" 2>&1)"; then
  echo "FAIL: Download helper accepted a directory as its output file." >&2; exit 1
fi
grep -q 'non-file destination' <<<"$output"
echo "PASS: Download-only extensions helper, pinned release, failure preservation, and cleanup."
