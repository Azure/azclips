# Version information - CLI
# Equivalent of: az version
#
# Prereq: none. Prints the CLI version, runtime, and configuration paths
# as structured JSON (like `az version`).

# Happy path: structured version information for the CLI host, loaded runtime,
# and configuration paths. Expect a JSON object with azCli, runtime, and paths.
version
