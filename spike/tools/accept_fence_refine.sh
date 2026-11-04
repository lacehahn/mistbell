#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
ROOT="$PWD"
mkdir -p spike/logs spike/artifacts/fence_refine
if [ "${1:-}" != --player-only ]; then
 /Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$ROOT/spike/project" -executeMethod FenceRefineBuild.Run -logFile "$ROOT/spike/logs/fence_refine_build.log" >/dev/null 2>&1
fi
caffeinate -dimu -t 900 &
CAFF=$!
trap 'kill "$CAFF" 2>/dev/null || true' EXIT
run_player(){
 local name="$1"; shift
 if pgrep -x Mistbell >/dev/null; then echo 'Another Mistbell player is running.' >&2; return 1; fi
 local log="$ROOT/spike/logs/fence_refine_${name}.log"
 rm -f "$log"
 open -n -a "$ROOT/spike/project/Builds/Mac/Mistbell.app" --args "$@" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$log"
 sleep 1
 for _ in $(seq 1 180); do
  if ! pgrep -x Mistbell >/dev/null; then echo "[RUN] $name exited log=$log"; return 0; fi
  sleep 1
 done
 echo "Player timeout: $log" >&2;return 124
}
rm -f spike/project/Shots/cam_play_1080.png spike/project/Shots/fence_closeup.png spike/project/Shots/fence_closeup_pixels.csv spike/project/Shots/fence_closeup_snow.csv
run_player camera -demo-camera-distance
run_player closeup -demo-fence-model
run_player walk -demo-walk 6
run_player interior -demo-enter-church
run_player reveal -demo-reveal
run_player bench -bench -bench-foreground -bench-output "$ROOT/spike/logs/fence_refine_bench.json"
python3 spike/tools/verify_fence_refine.py | tee "$ROOT/spike/logs/fence_refine_acceptance.log"
