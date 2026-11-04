#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
ROOT="$PWD"
mkdir -p spike/artifacts/path_texture spike/logs
if [ "${1:-}" != --player-only ]; then
 python3 - <<'PYASSET'
from pathlib import Path
from PIL import Image
src=Path('spike/art-prompts/staged');dst=Path('spike/project/Assets/Art/Textures/Path');dst.mkdir(parents=True,exist_ok=True)
for name in ['path_stone_a.jpg','path_stone_snow.jpg']:
 (dst/name).write_bytes((src/name).read_bytes())
Image.open(src/'path_stone_steps.jpg').crop((0,338,1280,450)).save(dst/'path_stone_steps_tread.png')
PYASSET
 /Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$ROOT/spike/project" -executeMethod PathTextureBuild.Run -logFile "$ROOT/spike/logs/path_texture_build.log" >/dev/null 2>&1
fi
caffeinate -dimu -t 900 &
CAFF=$!
trap 'kill "$CAFF" 2>/dev/null || true' EXIT
run_player(){
 local name="$1"; shift
 if pgrep -x Mistbell >/dev/null; then echo 'Another Mistbell player is running.' >&2; return 1; fi
 local log="$ROOT/spike/logs/path_texture_${name}.log"
 rm -f "$log"
 open -n -a "$ROOT/spike/project/Builds/Mac/Mistbell.app" --args "$@" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$log"
 sleep 1
 for _ in $(seq 1 180); do pgrep -x Mistbell >/dev/null || return 0; sleep 1; done
 echo "Player timeout: $log" >&2
 return 124
}
rm -f spike/project/Shots/path_closeup.png spike/project/Shots/path_lower.png spike/project/Shots/path_upper.png spike/project/Shots/path_steps.png spike/project/Shots/cam_play_1080.png
run_player camera -demo-camera-distance
run_player closeup -demo-path-texture
run_player walk -demo-walk 6
run_player interior -demo-enter-church
run_player reveal -demo-reveal
run_player bench -bench -bench-foreground -bench-output "$ROOT/spike/logs/path_texture_bench.json"
python3 spike/tools/verify_path_texture.py | tee "$ROOT/spike/logs/path_texture_acceptance.log"
