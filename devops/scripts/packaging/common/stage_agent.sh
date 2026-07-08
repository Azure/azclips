#!/usr/bin/env bash
# Splits the Agent daemon's self-contained publish output into a layout that
# co-locates the launcher with the CLI (so the runtime pack is shared) while
# keeping every agent-only asset under the Agent extension directory:
#
#   PublishDir/                             (co-located with CLI publish)
#     azclips-agent                         launcher: sibling to az so
#     azclips-agent.dll                       it shares the runtime pack
#     azclips-agent.deps.json                 and libhostpolicy.so/.dylib
#     azclips-agent.runtimeconfig.json        during Phase 1 native bootstrap
#     Extensions/Microsoft.Azure.Azclips.Extensions.Agent/
#       prompts/                            agent-only assets grouped
#       deps/                               under the Agent extension dir,
#         github.copilot.sdk/<ver>/         in NuGet-style layout so
#           lib/net10.0/GitHub.Copilot.SDK.dll        additionalProbingPaths
#           runtimes/<rid>/native/copilot            resolves them
#         <other agent-only nugets>/<ver>/lib/<tfm>/*.dll
#
# Runtime pack files (System.*.dll, libhostpolicy.so, etc.) come from the
# CLI's own publish and are byte-identical between the two publishes; this
# script deletes them from the agent publish output rather than duplicating.
#
# This is the sh counterpart of stage_agent.ps1; the two must stay in sync.

set -euo pipefail

usage() {
    echo "Usage: $0 --agent-publish-dir <DIR> --cli-publish-dir <DIR> --agent-extension-dir <DIR>" >&2
    exit 2
}

AGENT_PUBLISH_DIR=""
CLI_PUBLISH_DIR=""
AGENT_EXT_DIR=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --agent-publish-dir)   AGENT_PUBLISH_DIR="$2"; shift 2 ;;
        --cli-publish-dir)     CLI_PUBLISH_DIR="$2"; shift 2 ;;
        --agent-extension-dir) AGENT_EXT_DIR="$2"; shift 2 ;;
        *) usage ;;
    esac
done

[[ -z "$AGENT_PUBLISH_DIR" || -z "$CLI_PUBLISH_DIR" || -z "$AGENT_EXT_DIR" ]] && usage

for p in "$AGENT_PUBLISH_DIR" "$CLI_PUBLISH_DIR" "$AGENT_EXT_DIR"; do
    if [[ ! -d "$p" ]]; then
        echo "Directory not found: $p" >&2
        exit 1
    fi
done

if ! command -v python3 >/dev/null 2>&1; then
    echo "python3 is required for stage_agent" >&2
    exit 1
fi

DEPS_DIR="$AGENT_EXT_DIR/deps"
mkdir -p "$DEPS_DIR"

echo "Staging agent daemon into hybrid layout"
echo "  agent publish : $AGENT_PUBLISH_DIR"
echo "  CLI publish   : $CLI_PUBLISH_DIR"
echo "  extension dir : $AGENT_EXT_DIR"

# 1. Launcher files -> CLI publish root (sibling to az)
launcher_moved=0
for f in azclips-agent azclips-agent.exe azclips-agent.dll azclips-agent.deps.json azclips-agent.runtimeconfig.json; do
    src="$AGENT_PUBLISH_DIR/$f"
    if [[ -f "$src" ]]; then
        mv "$src" "$CLI_PUBLISH_DIR/$f"
        launcher_moved=$((launcher_moved + 1))
    fi
done
echo "  [1/6] launcher: moved $launcher_moved files to CLI publish root"

# 2. prompts/ -> extension dir
if [[ -d "$AGENT_PUBLISH_DIR/prompts" ]]; then
    prompts_dst="$AGENT_EXT_DIR/prompts"
    [[ -d "$prompts_dst" ]] && rm -rf "$prompts_dst"
    mv "$AGENT_PUBLISH_DIR/prompts" "$prompts_dst"
    echo "  [2/6] prompts/: moved to $prompts_dst"
fi

# 3-5. Parse deps.json, then classify/move/dedup remaining files.
#      The classification logic is identical to stage_agent.ps1; we drive it
#      from python so we don't have to reimplement JSON traversal in bash.
DEPS_JSON="$CLI_PUBLISH_DIR/azclips-agent.deps.json"
if [[ ! -f "$DEPS_JSON" ]]; then
    echo "azclips-agent.deps.json not at $DEPS_JSON after launcher move" >&2
    exit 1
fi

python3 - "$AGENT_PUBLISH_DIR" "$CLI_PUBLISH_DIR" "$DEPS_DIR" "$DEPS_JSON" <<'PY'
import hashlib, json, os, shutil, sys

agent_publish, cli_publish, deps_dir, deps_json = sys.argv[1:5]

def sha(p):
    h = hashlib.sha256()
    with open(p, 'rb') as f:
        for chunk in iter(lambda: f.read(1024*1024), b''):
            h.update(chunk)
    return h.hexdigest()

deps = json.load(open(deps_json))

dll_map = {}                # dll basename -> (pkg, ver, rel_path)
runtimepack_names = set()   # dll basenames that come from the .NET runtime pack
sdk_version = None

for tfm, tge in deps['targets'].items():
    for lib_id, lib in tge.items():
        if lib_id.startswith('runtimepack.'):
            for section in ('runtime', 'native'):
                if section in lib:
                    for fp in lib[section]:
                        runtimepack_names.add(os.path.basename(fp))
            continue
        if '/' not in lib_id:
            continue
        pkg, ver = lib_id.rsplit('/', 1)
        # Skip .Reference libs (compile-time only, real dll comes from the
        # non-.Reference twin, e.g. Azclips.Agent.Tools/1.0.0 vs
        # Azclips.Agent.Tools.Reference/1.0.0.0).
        if pkg.endswith('.Reference'):
            continue
        if pkg == 'GitHub.Copilot.SDK':
            sdk_version = ver
        if 'runtime' in lib:
            for fp in lib['runtime']:
                dll_map[os.path.basename(fp)] = (pkg, ver, fp)

if not sdk_version:
    print("Could not find GitHub.Copilot.SDK version in azclips-agent.deps.json", file=sys.stderr)
    sys.exit(1)

# 3. Copilot native binary -> deps/github.copilot.sdk/<ver>/runtimes/<rid>/native/
copilot_moved = 0
native_root = os.path.join(agent_publish, 'runtimes')
if os.path.isdir(native_root):
    for rid_dir in os.listdir(native_root):
        native_src_dir = os.path.join(native_root, rid_dir, 'native')
        if not os.path.isdir(native_src_dir):
            continue
        for name in os.listdir(native_src_dir):
            if name not in ('copilot.exe', 'copilot'):
                continue
            src = os.path.join(native_src_dir, name)
            dst_dir = os.path.join(deps_dir, 'github.copilot.sdk', sdk_version,
                                   'runtimes', rid_dir, 'native')
            os.makedirs(dst_dir, exist_ok=True)
            shutil.move(src, os.path.join(dst_dir, name))
            copilot_moved += 1
print(f'  [3/6] copilot native: moved {copilot_moved} files to '
      f'deps/github.copilot.sdk/{sdk_version}/runtimes/<rid>/native/')

# 4-5. Remaining top-level files in agent publish:
runtimepack_deduped = 0
nuget_moved = 0
purged = 0
unclassified = []

for name in os.listdir(agent_publish):
    src = os.path.join(agent_publish, name)
    if not os.path.isfile(src):
        continue

    if name in runtimepack_names:
        root_twin = os.path.join(cli_publish, name)
        if os.path.isfile(root_twin) and sha(src) == sha(root_twin):
            os.remove(src)
            runtimepack_deduped += 1
        else:
            # Runtime pack member missing at CLI root or hash differs — this
            # is a build-time inconsistency that would break the agent at
            # runtime; keep it loud so packaging fails audibly.
            unclassified.append(
                f"{name} (runtime pack, differs from CLI root - publish version drift?)")
        continue

    if name in dll_map:
        pkg, ver, rel = dll_map[name]
        dst_dir = os.path.join(deps_dir, pkg.lower(), ver, os.path.dirname(rel))
        os.makedirs(dst_dir, exist_ok=True)
        shutil.move(src, os.path.join(dst_dir, name))
        nuget_moved += 1
        continue

    if name.endswith('.pdb') or name == '.env' or name.endswith('.dll.config'):
        os.remove(src)
        purged += 1
        continue

    unclassified.append(name)

print(f'  [4/6] runtime pack: deduped {runtimepack_deduped} files (byte-identical to CLI root)')
print(f'  [5/6] agent NuGet: moved {nuget_moved} dlls to deps/<pkg>/<ver>/<lib-path>/')
if purged:
    print(f'        (purged {purged} non-shipping files: pdb / .env / dll.config)')

# 6. Clean up the Agent extension top level: drop dlls redundant with either
#    the CLI root or deps/ (extension loader will fall back), and non-shipping
#    artifacts (.pdb / .dll.config / stray runtimeconfig). Match stage_agent.ps1.
keep_names = {
    'Microsoft.Azure.Azclips.Extensions.Agent.dll',
    'Microsoft.Azure.Azclips.Extensions.Agent.deps.json',
}
ext_cleaned = 0

# Cache hashes of files under deps/ so the extension-top scan can compare
# without walking the tree per file.
deps_hash_by_name = {}
for root, _, files in os.walk(deps_dir):
    for f in files:
        deps_hash_by_name.setdefault(f, []).append(os.path.join(root, f))

for name in os.listdir(agent_ext := os.path.dirname(deps_dir)):
    p = os.path.join(agent_ext, name)
    if not os.path.isfile(p):
        continue
    if name in keep_names:
        continue

    deletable = False
    if (name.endswith('.pdb') or name.endswith('.dll.config') or
        name == 'Microsoft.Azure.Azclips.Extensions.Agent.runtimeconfig.json'):
        deletable = True
    elif name.endswith('.dll'):
        root_twin = os.path.join(cli_publish, name)
        if os.path.isfile(root_twin) and sha(p) == sha(root_twin):
            deletable = True
        else:
            top_hash = sha(p)
            for candidate in deps_hash_by_name.get(name, []):
                if sha(candidate) == top_hash:
                    deletable = True
                    break

    if deletable:
        os.remove(p)
        ext_cleaned += 1

# Also drop the netstandard fallback runtimes/ subdir at extension top: it
# duplicates the file already staged under deps/<pkg>/.../runtimes/.
ext_runtimes = os.path.join(agent_ext, 'runtimes')
if os.path.isdir(ext_runtimes):
    shutil.rmtree(ext_runtimes)
    ext_cleaned += 1

print(f'  [6/6] extension top-level: removed {ext_cleaned} redundant files')

if unclassified:
    print('')
    print('WARNING: unclassified agent publish outputs. Verify packaging is not silently dropping something needed:')
    for u in unclassified:
        print(f'  - {u}')

print('OK: stage_agent complete')
PY

# Clean up empty subdirs left behind (runtimes/, empty prompts/, etc.)
find "$AGENT_PUBLISH_DIR" -type d -empty -delete 2>/dev/null || true
