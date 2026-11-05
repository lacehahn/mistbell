#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
ROOT="$PWD"
mkdir -p spike/artifacts/church3d spike/logs
if [[ "${1:-}" != --player-only ]]; then
 python3 spike/tools/church3d_textures.py
 /Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$ROOT/spike/project" -executeMethod Church3DDetailBuild.Run -logFile "$ROOT/spike/logs/church3d_build.log" >/dev/null 2>&1
fi
if pgrep -x Mistbell >/dev/null; then echo 'Mistbell already running' >&2; exit 1; fi
caffeinate -dimu -t 900 &
CAFF=$!
trap 'kill "$CAFF" 2>/dev/null || true' EXIT
run_player(){
 local log="$1"; shift
 open -n -a "$ROOT/spike/project/Builds/Mac/Mistbell.app" --args "$@" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$ROOT/spike/logs/church3d_${log}.log"
 sleep 1
 for _ in $(seq 1 180); do pgrep -x Mistbell >/dev/null || return 0; sleep 1; done
 echo "timeout: $log" >&2; return 124
}
run_player walk -demo-walk 6
run_player interior -demo-enter-church
run_player reveal -demo-reveal
run_player capture -shot "$ROOT/spike/project/Shots/art_runtime.png" -church3d-shots
run_player bench -bench -bench-baseline -bench-output "$ROOT/spike/artifacts/church3d/bench.json"
python3 spike/tools/verify_church3d.py
