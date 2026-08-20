#!/usr/bin/env sh
set -eu

if [ -e /usr/bin/az ] || [ -L /usr/bin/az ]; then
  owner="$(dpkg-query -S /usr/bin/az 2>/dev/null | sed -n '1s/:.*//p')"
  case "$owner" in
    azcli)
      ;;
    "")
      echo "ERROR: /usr/bin/az already exists and is not owned by a Debian package." >&2
      echo "Remove or relocate it before installing azcli." >&2
      exit 1
      ;;
    *)
      echo "ERROR: /usr/bin/az is owned by package '$owner'." >&2
      echo "Remove the conflicting package explicitly before installing azcli." >&2
      exit 1
      ;;
  esac
fi