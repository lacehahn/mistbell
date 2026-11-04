#!/usr/bin/env bash
set -eu
cd "$(dirname "$0")/../.."
ROOT="$PWD"
mkdir -p spike/logs spike/artifacts/camera_distance
if [ "${1:-}" != --player-only ]; then
 /Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$ROOT/spike/project" -executeMethod CameraDistanceBuild.Run -logFile "$ROOT/spike/logs/camera_distance_build.log" >/dev/null 2>&1
fi
caffeinate -dimu -t 900 &
CAFF=$!
trap 'kill "$CAFF" 2>/dev/null || true' EXIT
run_player(){
 local log="$1"; shift
 if pgrep -x Mistbell >/dev/null; then echo 'Mistbell already running' >&2; return 1; fi
 open -n -a "$ROOT/spike/project/Builds/Mac/Mistbell.app" --args "$@" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$log"
 sleep 1
 for _ in $(seq 1 150); do pgrep -x Mistbell >/dev/null || return 0; sleep 1; done
 echo "Player timeout: $log" >&2
 return 124
}
run_player "$ROOT/spike/logs/camera_distance_capture.log" -demo-camera-distance
run_player "$ROOT/spike/logs/camera_distance_walk.log" -demo-walk 6
run_player "$ROOT/spike/logs/camera_distance_interior.log" -demo-enter-church
run_player "$ROOT/spike/logs/camera_distance_reveal.log" -demo-reveal
run_player "$ROOT/spike/logs/camera_distance_bench.log" -bench -bench-foreground -bench-output "$ROOT/spike/logs/camera_distance_bench.json"
python3 spike/tools/verify_camera_distance.py
