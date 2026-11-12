#!/usr/bin/env bash
#
# Download only: extraction and az extension add remain explicit user steps.
main() {
  set -euo pipefail
  release_tag=""
  stage=""
  die() { echo "ERROR: $*" >&2; exit 1; }
  cleanup() { [[ -z "$stage" ]] || rm -rf "$stage"; }
  trap cleanup EXIT

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --release)
        [[ $# -ge 2 && -n "$2" ]] || die "--release requires a release tag."
        release_tag="$2"; shift 2 ;;
      -h|--help)
        echo "Usage: azcli-extensions-download.sh [--release <tag>]"
        echo "Downloads azcli-extensions.tar.gz into the current directory; does not extract or install."
        return ;;
      *) die "Unknown argument: $1 (use --help)." ;;
    esac
  done
  [[ -z "$release_tag" || "$release_tag" =~ ^[A-Za-z0-9._-]+$ ]] ||
    die "Invalid release tag. Use letters, digits, dots, underscores, and hyphens."
  [[ ! -L ./azcli-extensions.tar.gz && (! -e ./azcli-extensions.tar.gz || -f ./azcli-extensions.tar.gz) ]] ||
    die "Refusing to replace a non-file destination: ./azcli-extensions.tar.gz."
  command -v curl >/dev/null 2>&1 || die "curl is required for public downloads."
  endpoint="https://api.github.com/repos/Azure/azclips/releases/latest"
  [[ -z "$release_tag" ]] || endpoint="https://api.github.com/repos/Azure/azclips/releases/tags/$release_tag"
  stage="$(mktemp -d "./.azcli-extensions-download.XXXXXX")"
  if ! http_status="$(curl --fail --show-error --silent --location \
    --write-out '%{http_code}' "$endpoint" -o "$stage/release.json")"; then
    if [[ -n "$release_tag" && "$http_status" == 404 ]]; then
      die "Release tag '$release_tag' was not found in Azure/azclips."
    fi
    die "Could not retrieve the release. Check network access and GitHub's anonymous API rate limit."
  fi
  tr -d '\r\n' < "$stage/release.json" > "$stage/release-line.txt"
  # Include the closing quote so a URL with a query or extra path cannot match a prefix.
  grep -oE '"browser_download_url"[[:space:]]*:[[:space:]]*"https://github[.]com/Azure/azclips/releases/download/[A-Za-z0-9._-]+/azcli-extensions-[0-9][A-Za-z0-9._-]*[.]tar[.]gz"' \
    "$stage/release-line.txt" > "$stage/download-fields.txt" || {
      [[ $? -eq 1 ]] || die "Could not read release download links."
    }
  matches=0
  url=""
  while IFS= read -r field; do
    candidate_url="https://${field#*\"https://}"
    candidate_url="${candidate_url%\"}"
    candidate_release="${candidate_url%/*}"
    candidate_release="${candidate_release##*/}"
    [[ -z "$release_tag" || "$candidate_release" == "$release_tag" ]] || continue
    matches=$((matches + 1))
    url="$candidate_url"
  done < "$stage/download-fields.txt"
  [[ "$matches" -eq 1 ]] || die "Expected exactly one extensions bundle download URL; found $matches."
  curl --fail --show-error --location "$url" -o "$stage/azcli-extensions.tar.gz" ||
    die "Could not download the extensions bundle. Check network access and retry."
  [[ -f "$stage/azcli-extensions.tar.gz" ]] || die "Download did not produce the expected archive."
  mv -f "$stage/azcli-extensions.tar.gz" ./azcli-extensions.tar.gz
  echo "Downloaded: $url"
  echo "Saved to: ./azcli-extensions.tar.gz (not extracted or installed)"
}

main "$@"
