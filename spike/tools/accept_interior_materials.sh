#!/usr/bin/env bash
# Local material patch, then actual 1080p player captures and a 300-frame benchmark.
set -eu
cd "$(dirname "$0")/../.."
ROOT="$PWD"
UNITY=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
mkdir -p spike/logs spike/artifacts/interior_materials
if [ "${1:-}" != "--capture-only" ]; then
    "$UNITY" -batchmode -quit -projectPath "$ROOT/spike/project" -executeMethod InteriorMaterialsBuild.Run -logFile "$ROOT/spike/logs/interior_materials_build.log" >/dev/null 2>&1
fi
caffeinate -dimu -t 600 &
CAFF=$!
trap 'kill "$CAFF" 2>/dev/null || true' EXIT
run_player() {
    local log="$1"
    shift
    if pgrep -x Mistbell >/dev/null; then echo 'Another Mistbell player is running.' >&2; return 1; fi
    open -n -a "$ROOT/spike/project/Builds/Mac/Mistbell.app" --args "$@" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$log"
    sleep 1
    for _ in $(seq 1 150); do pgrep -x Mistbell >/dev/null || return 0; sleep 1; done
    return 124
}
run_player "$ROOT/spike/logs/interior_materials_interior.log" -demo-enter-church
run_player "$ROOT/spike/logs/interior_materials_altar.log" -demo-altar
run_player "$ROOT/spike/logs/interior_materials_bench.log" -bench -bench-foreground -bench-output "$ROOT/spike/logs/interior_materials_bench.json"
/usr/bin/python3 spike/tools/verify_interior_materials.py
