#!/usr/bin/env bash
# Only the interior_windows task: local patch, build, 1080p walkthrough, benchmark, independent PNG/asset verification.
set -eu
cd "$(dirname "$0")/../.."
ROOT="$PWD"
UNITY=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
mkdir -p spike/logs spike/artifacts/interior_windows
"$UNITY" -batchmode -quit -projectPath "$ROOT/spike/project" -executeMethod InteriorWindowsBuild.Run -logFile "$ROOT/spike/logs/interior_windows_build.log" >/dev/null 2>&1
caffeinate -dimu -t 600 &
CAFF=$!
trap 'kill "$CAFF" 2>/dev/null || true' EXIT
run_player() {
    local log="$1"
    shift
    if pgrep -x Mistbell >/dev/null; then echo 'Another Mistbell player is already running.' >&2; return 1; fi
    open -n -a "$ROOT/spike/project/Builds/Mac/Mistbell.app" --args "$@" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$log"
    sleep 1
    for _ in $(seq 1 150); do pgrep -x Mistbell >/dev/null || return 0; sleep 1; done
    echo "Player timed out: $log" >&2
    return 124
}
run_player "$ROOT/spike/logs/interior_windows_interior.log" -demo-enter-church
run_player "$ROOT/spike/logs/interior_windows_bench.log" -bench -bench-foreground -bench-output "$ROOT/spike/logs/interior_windows_bench.json"
/usr/bin/python3 spike/tools/verify_interior_windows.py
