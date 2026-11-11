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
#       deps/                               under the Agent extension dir:
#         <agent-only nugets>/<ver>/lib/<tfm>/*.dll   NuGet-style so
#         runtimes/<rid>/native/copilot                additionalProbingPaths
#                                                      + SdkNativeDir resolve them
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
agent_ext_dir = os.path.dirname(deps_dir)   # extension top level (deps/ is under it)

def sha(p):
    h = hashlib.sha256()
    with open(p, 'rb') as f:
        for chunk in iter(lambda: f.read(1024*1024), b''):
            h.update(chunk)
    return h.hexdigest()

deps = json.load(open(deps_json))

# hostfxr resolves the agent subprocess's deps two ways:
#   - NuGet packages: probed under deps/ (from additionalProbingPaths) using the
#     SDK-lowercased libraries[].path — deps/<path>/ is the only home they need.
#   - project refs (libraries[].path == null): the deps/ probing path does NOT
#     actually help these (hostfxr appends <id>/<ver>/ there, but more to the
#     point the CLI already carries them). They resolve from the APP BASE dir —
#     which after staging is the CLI publish root, right next to azclips-agent.
#     Every project ref EXCEPT the agent-only Azclips.Agent.Tools already has a
#     byte-identical copy in the CLI root, which is why only that one dll goes
#     missing. So: still lay project refs under deps/ (correct-cased, harmless),
#     AND copy any project ref that the CLI root lacks INTO the CLI root, so the
#     app-base probe finds it just like the other project dlls.
libraries = deps.get('libraries', {})
def probe_subdir(lib_id, pkg, ver):
    lib = libraries.get(lib_id) or {}
    path = lib.get('path')
    if path:                      # NuGet package: SDK-provided (lowercased) path
        return path.replace('\\', '/')
    return f'{pkg}/{ver}'         # project ref: original-case id/version

def is_project_ref(lib_id):
    return (libraries.get(lib_id) or {}).get('type') == 'project'

dll_map = {}                # dll basename -> (probe_subdir, rel_path, is_project)
runtimepack_names = set()   # dll basenames that come from the .NET runtime pack

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
        if 'runtime' in lib:
            for fp in lib['runtime']:
                dll_map[os.path.basename(fp)] = (probe_subdir(lib_id, pkg, ver), fp, is_project_ref(lib_id))

# 4-5. Remaining top-level files in agent publish:
runtimepack_deduped = 0
nuget_moved = 0
purged = 0
unclassified = []
flat_kept_at_ext_top = set()   # agent-only project dlls we copy flat to ext top

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
        subdir, rel, is_proj = dll_map[name]
        # An agent-only project ref (only Azclips.Agent.Tools today) must satisfy
        # BOTH loaders, which look in different flat dirs:
        #   - agent subprocess: app base = CLI root (flat) → copy there.
        #   - `az agent tool ...` CLI extension ALC: extension top level (flat)
        #     → copy there too.
        # Copy to whichever of those two lacks the dll (from the publish src),
        # THEN move the src down into deps/ (correct-cased; harmless, keeps the
        # NuGet-style tree complete). Project refs the CLI root already carries
        # need neither copy — the copy is skipped when the twin exists.
        if is_proj:
            for flat_dir in (cli_publish, agent_ext_dir):
                twin = os.path.join(flat_dir, name)
                if not os.path.isfile(twin):
                    shutil.copy2(src, twin)
            # Remember the ones we placed flat at the extension top level so the
            # step-6 cleanup below doesn't delete them as "redundant with deps/".
            flat_kept_at_ext_top.add(name)
        # subdir already carries the correctly-cased <pkg>/<ver> hostfxr probes;
        # rel's directory part is the lib-relative path (e.g. lib/<tfm>).
        dst_dir = os.path.join(deps_dir, subdir, os.path.dirname(rel))
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

# 6. Clean up the Agent extension top level: drop dlls redundant with the
#    CLI root (extension loader will fall back), and non-shipping
#    artifacts (.pdb / .dll.config / stray runtimeconfig). Match stage_agent.ps1.
keep_names = {
    'Microsoft.Azure.Azclips.Extensions.Agent.dll',
    'Microsoft.Azure.Azclips.Extensions.Agent.deps.json',
}
ext_cleaned = 0

for name in os.listdir(agent_ext := os.path.dirname(deps_dir)):
    p = os.path.join(agent_ext, name)
    if not os.path.isfile(p):
        continue
    if name in keep_names:
        continue
    # Agent-only project dlls we deliberately placed flat here (for the CLI
    # extension loader) must NOT be cleaned up, even though deps/ has a twin.
    if name in flat_kept_at_ext_top:
        continue

    deletable = False
    if (name.endswith('.pdb') or name.endswith('.dll.config') or
        name == 'Microsoft.Azure.Azclips.Extensions.Agent.runtimeconfig.json'):
        deletable = True
    elif name.endswith('.dll'):
        root_twin = os.path.join(cli_publish, name)
        # A twin under deps/ does not count: the extension loader never
        # probes deps/.
        if os.path.isfile(root_twin) and sha(p) == sha(root_twin):
            deletable = True

    if deletable:
        os.remove(p)
        ext_cleaned += 1

# Drop the netstandard fallback runtimes/ subdir wholesale: every native it
# carries is already staged under deps/<pkg>/.../runtimes/. Count the files
# removed so the [6/6] tally is accurate.
ext_runtimes = os.path.join(agent_ext, 'runtimes')
if os.path.isdir(ext_runtimes):
    ext_cleaned += sum(len(files) for _, _, files in os.walk(ext_runtimes))
    shutil.rmtree(ext_runtimes)

print(f'  [6/6] extension top-level: removed {ext_cleaned} redundant files')

# Any directory still at the agent-publish top level is one not handled above
# (prompts/ was moved in step 2; runtimes/ and anything new land here). Move
# each verbatim under deps/<name>/ so it ships instead of being silently
# dropped — the directory-side counterpart of the unclassified-file check
# below. runtimes/<rid>/native/copilot then resolves via AgentInstallLayout.
dirs_moved = 0
for name in os.listdir(agent_publish):
    src = os.path.join(agent_publish, name)
    if not os.path.isdir(src):
        continue
    dst = os.path.join(deps_dir, name)
    if os.path.isdir(dst):
        shutil.rmtree(dst)
    shutil.move(src, dst)
    dirs_moved += 1
if dirs_moved:
    print(f'  [6/6] extra dirs: moved {dirs_moved} top-level dir(s) to deps/ verbatim')

if unclassified:
    print('')
    print('ERROR: unclassified agent publish outputs. Packaging cannot silently drop or ship unknown files:', file=sys.stderr)
    for u in unclassified:
        print(f'  - {u}', file=sys.stderr)
    sys.exit(1)

print('OK: stage_agent complete')
PY

# Clean up empty subdirs left behind (runtimes/, empty prompts/, etc.)
find "$AGENT_PUBLISH_DIR" -type d -empty -delete 2>/dev/null || true
