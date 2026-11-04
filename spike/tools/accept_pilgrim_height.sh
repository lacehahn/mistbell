#!/usr/bin/env bash
# Task-specific acceptance. Check positive terminal markers, including gate=FAIL missed by run_demos.sh.
set -eu
cd "$(dirname "$0")/../.."
ROOT="$PWD"
mkdir -p spike/logs spike/artifacts/pilgrim_height
if [ "${1:-}" != --player-only ]; then
 /Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$ROOT/spike/project" -executeMethod PilgrimHeightBuild.Run -logFile "$ROOT/spike/logs/pilgrim_height_build.log" >/dev/null 2>&1
fi
caffeinate -dimu -t 600 &
CAFF=$!
trap 'kill "$CAFF" 2>/dev/null || true' EXIT
run_player(){
 local log="$1"; shift
 if pgrep -x Mistbell >/dev/null; then echo 'Mistbell already running' >&2; return 1; fi
 open -n -a "$ROOT/spike/project/Builds/Mac/Mistbell.app" --args "$@" -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "$log"
 sleep 1
 for _ in $(seq 1 120); do pgrep -x Mistbell >/dev/null || return 0; sleep 1; done
 return 124
}
run_player "$ROOT/spike/logs/pilgrim_height_interior.log" -demo-enter-church
run_player "$ROOT/spike/logs/pilgrim_height_walk.log" -demo-walk 6
run_player "$ROOT/spike/logs/pilgrim_height_reveal.log" -demo-reveal
python3 spike/tools/verify_pilgrim_height.py
