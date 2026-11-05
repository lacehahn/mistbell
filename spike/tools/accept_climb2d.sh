#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
ROOT="$PWD"
mkdir -p spike/logs spike/artifacts/climb2d
if [[ "${1:-}" != --player-only ]]; then
 /Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$ROOT/spike/project" -executeMethod Climb2DBuild.Run -logFile "$ROOT/spike/logs/climb2d_build.log" >/dev/null 2>&1
fi
if pgrep -x Mistbell >/dev/null; then echo 'Mistbell already running; close it before acceptance.' >&2; exit 1; fi
caffeinate -dimu -t 600 &
CAFF=$!
trap 'kill "$CAFF" 2>/dev/null || true' EXIT
run_player() {
 local log="$1"; shift
 open -n -a "$ROOT/spike/project/Builds/Mac/Mistbell.app" --args "$@" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$log"
 sleep 2
 for _ in $(seq 1 120); do pgrep -x Mistbell >/dev/null || return 0; sleep 1; done
 echo "timeout: $log" >&2; return 124
}
rm -f spike/artifacts/climb2d/climb2d_*.png spike/artifacts/climb2d/arrival_3d.png
run_player "$ROOT/spike/logs/climb2d_run.log" -demo-climb2d -climb2d-auto -climb2d-output "$ROOT/spike/artifacts/climb2d"
run_player "$ROOT/spike/logs/climb2d_walk.log" -demo-walk 6
run_player "$ROOT/spike/logs/climb2d_stairs.log" -demo-walk-stairs
run_player "$ROOT/spike/logs/climb2d_camera.log" -demo-camera-distance
python3 spike/tools/verify_climb2d.py
