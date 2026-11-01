#!/bin/bash
# 运行已构建的播放器，遍历场景 ShotRig 的机位表逐张截图，带看门狗（90 秒后强杀）
set -u
cd "$(dirname "$0")/../project" || exit 1
APP="./Builds/Mac/Mistbell.app/Contents/MacOS/Mistbell"
DIR="$PWD/Shots"
LOG="$PWD/../logs/20_runtime_$(date +%H%M%S).log"

[ -x "$APP" ] || { echo "播放器不存在，先跑 SpikeBuild.Run"; exit 1; }
mkdir -p "$DIR"

"$APP" -shots "$DIR" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$LOG" &
PID=$!
for _ in $(seq 1 90); do
  kill -0 "$PID" 2>/dev/null || break
  sleep 1
done
kill "$PID" 2>/dev/null
wait "$PID" 2>/dev/null

echo "LOG=$LOG"
grep -E '^\[SHOT\]|^\[SPIKE\]' "$LOG" || true
echo "--- files ---"
ls -lh "$DIR"/art_*.png 2>/dev/null || echo "无截图产出"
