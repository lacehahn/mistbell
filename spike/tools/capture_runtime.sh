#!/bin/bash
# 运行已构建的播放器，遍历场景 ShotRig 的机位表逐张截图，带看门狗（90 秒后强杀）
set -u
# --single captures the current scene camera without the multi-pose pilgrim regression gate.
cd "$(dirname "$0")/../project" || exit 1
APP="./Builds/Mac/Mistbell.app/Contents/MacOS/Mistbell"
DIR="$PWD/Shots"
LOG="$PWD/../logs/20_runtime_$(date +%H%M%S).log"

[ -x "$APP" ] || { echo "播放器不存在，先跑 SpikeBuild.Run"; exit 1; }
mkdir -p "$DIR"

SHOT_ARGS=(-shots "$DIR")
if [ "${1:-}" = "--single" ]; then
  SHOT_ARGS=(-shot "$DIR/art_runtime.png")
fi

# Optional workaround for the macOS Metal worker deadlock; camera/renderer settings are unchanged.
GFX_ARGS=()
if [ "${MISTBELL_GFX_DIRECT:-0}" = "1" ]; then GFX_ARGS=(-force-gfx-direct); fi
"$APP" "${GFX_ARGS[@]}" "${SHOT_ARGS[@]}" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$LOG" &
PID=$!
for _ in $(seq 1 90); do
  kill -0 "$PID" 2>/dev/null || break
  sleep 1
done
TIMED_OUT=0
if kill -0 "$PID" 2>/dev/null; then
  TIMED_OUT=1
  kill "$PID" 2>/dev/null
fi
wait "$PID" 2>/dev/null
RC=$?
if [ "$TIMED_OUT" -eq 1 ]; then RC=124; fi

echo "LOG=$LOG"
grep -E '^\[SHOT\]|^\[SPIKE\]' "$LOG" || true
echo "--- files ---"
ls -lh "$DIR"/art_*.png 2>/dev/null || echo "无截图产出"

exit "$RC"
