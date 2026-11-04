#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
ROOT="$PWD"
mkdir -p spike/logs spike/artifacts/path_scale_tone
if [ "${1:-}" != --player-only ]; then
 /Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$ROOT/spike/project" -executeMethod PathScaleToneBuild.Run -logFile "$ROOT/spike/logs/path_scale_tone_build.log" >/dev/null 2>&1
fi
caffeinate -dimu -t 900 &
CAFF=$!
trap 'kill "$CAFF" 2>/dev/null || true' EXIT
run_player(){
 local name="$1"; shift
 if pgrep -x Mistbell >/dev/null; then echo 'Another Mistbell player is running.' >&2; return 1; fi
 local log="$ROOT/spike/logs/path_scale_tone_${name}.log"
 rm -f "$log"
 open -n -a "$ROOT/spike/project/Builds/Mac/Mistbell.app" --args "$@" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$log"
 sleep 1
 for _ in $(seq 1 180); do
  if ! pgrep -x Mistbell >/dev/null; then echo "[RUN] $name exited log=$log"; return 0; fi
  sleep 1
 done
 echo "Player timeout: $log" >&2;return 124
}
rm -f spike/project/Shots/{cam_play_1080,path_closeup}{.png,_tone_pixels.csv}
run_player camera -demo-camera-distance -path-scale-tone-audit
run_player closeup -demo-path-texture -path-scale-tone-audit
run_player fence -demo-fence-model
run_player walk -demo-walk 6
run_player interior -demo-enter-church
run_player reveal -demo-reveal
run_player bench -bench -bench-foreground -bench-output "$ROOT/spike/logs/path_scale_tone_bench.json"
python3 spike/tools/verify_path_scale_tone.py | tee "$ROOT/spike/logs/path_scale_tone_acceptance.log"
