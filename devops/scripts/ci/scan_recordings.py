#!/usr/bin/env python3

# Pre-merge secret scan for scenario-test recordings (SessionRecords/*.json).
#
# The recorder sanitises recordings before they are written (subscription and
# tenant ids become the all-zero placeholder, bearer tokens are redacted, the
# Authorization and api-key headers are dropped). This script is the second
# layer: it scans the recordings a PR adds or modifies and fails if anything
# slipped through, so a bad recording cannot merge.
#
# Follows the incremental only-scan-changed-recordings approach Azure CLI uses
# for its recordings. Their implementation targets vcrpy YAML recordings and
# their own credential handler, so the rules here are written for our JSON
# recording format instead of wrapping their tool.
#
# Usage:
#   scan_recordings.py --base origin/main    scan recordings changed since base
#   scan_recordings.py --all                 scan every recording in the repo
#   scan_recordings.py <file> [<file> ...]   scan the given files

import re
import subprocess
import sys

PLACEHOLDER_GUID = "00000000-0000-0000-0000-000000000000"
GUID = r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"

# A double quote as it can appear inside a recorded body: bare, backslash-escaped,
# or unicode-escaped the way System.Text.Json writes it (backslash-u0022).
Q = r'(?:\\u0022|\\"|")'

RECORDING_PATH = re.compile(r"^tests/.+/SessionRecords/.+\.json$")

# Rule name -> pattern that must not appear in a sanitised recording. Patterns
# are matched per line; findings report the file, line and rule, never the
# matched value itself.
RULES = {
    "authorization header": re.compile(r'"Authorization"\s*:', re.IGNORECASE),
    "api-key header": re.compile(r'"api-key"\s*:', re.IGNORECASE),
    "bearer token": re.compile(r"Bearer\s+(?!\[redacted\])\S+"),
    "jwt": re.compile(r"eyJ[A-Za-z0-9_\-]{20,}"),
    "subscription id": re.compile(r"/subscriptions/(?!" + PLACEHOLDER_GUID + ")" + GUID, re.IGNORECASE),
    "tenant id": re.compile(r"/tenants/(?!" + PLACEHOLDER_GUID + ")" + GUID, re.IGNORECASE),
    "aad authority tenant": re.compile(r"login\.microsoftonline\.com/(?!" + PLACEHOLDER_GUID + ")" + GUID, re.IGNORECASE),
    "tenant or object id pair": re.compile(r"(tenantId|objectId)=(?!" + PLACEHOLDER_GUID + ")" + GUID, re.IGNORECASE),
    "storage account key": re.compile(r"AccountKey=", re.IGNORECASE),
    "shared access signature": re.compile(r"SharedAccessSignature|[?&]sig=", re.IGNORECASE),
    "operation URL signature": re.compile(
        r"/operation(?:results|statuses)/.*(?:\\u0026|[?&])[csh]="
        r"(?!sanitized(?:\\u0026|&|\"|\\|$))[A-Za-z0-9_\-%.=]+",
        re.IGNORECASE),
    "client secret": re.compile(Q + "client_secret" + Q + r"\s*:\s*" + Q + r"(?!\[redacted\])[^\"\\]+", re.IGNORECASE),
    "password": re.compile(Q + "(adminPassword|password)" + Q + r"\s*:\s*" + Q + r"(?!\[redacted\])[^\"\\]+", re.IGNORECASE),
    "access token field": re.compile(Q + "(access_token|accessToken)" + Q + r"\s*:\s*" + Q + r"(?!\[redacted\])[^\"\\]+", re.IGNORECASE),
    "openai key": re.compile(r"sk-[A-Za-z0-9_\-]{16,}"),
    "openai endpoint": re.compile(r"https://[a-z0-9][a-z0-9-]*\.openai\.azure\.com", re.IGNORECASE),
}


def error(message):
    print(f"ERROR: {message}", file=sys.stderr)


def changed_recordings(base):
    diff = subprocess.run(
        ["git", "diff", "--name-only", "--diff-filter=ACMR", f"{base}...HEAD"],
        capture_output=True, text=True, check=True)
    return [f for f in diff.stdout.splitlines() if RECORDING_PATH.match(f)]


def all_recordings():
    files = subprocess.run(
        ["git", "ls-files", "--", "tests"],
        capture_output=True, text=True, check=True)
    return [f for f in files.stdout.splitlines() if RECORDING_PATH.match(f)]


def scan(path):
    findings = 0
    try:
        with open(path, encoding="utf-8") as f:
            lines = f.readlines()
    except (OSError, UnicodeError) as e:
        error(f"{path}: cannot read: {e}")
        return 1

    for number, line in enumerate(lines, start=1):
        for rule, pattern in RULES.items():
            if pattern.search(line):
                error(f"{path}:{number}: possible {rule} in recording")
                findings += 1
    return findings


def main(argv):
    if argv and argv[0] == "--all":
        files = all_recordings()
    elif argv and argv[0] == "--base":
        base = argv[1] if len(argv) > 1 and argv[1] else "origin/main"
        files = changed_recordings(base)
    elif argv:
        files = argv
    else:
        files = changed_recordings("origin/main")

    if not files:
        print("No recordings to scan.")
        return 0

    findings = 0
    for path in files:
        findings += scan(path)

    if findings:
        error(f"{findings} possible secret(s) in {len(files)} recording(s). "
              "Re-record the scenario so the sanitisers scrub them, or extend "
              "RecordingSanitizers if a new secret shape appeared.")
        return 1

    print(f"Scanned {len(files)} recording(s), no secrets found.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
