#!/bin/bash
# 运行已构建的播放器并截一张图，带看门狗（40 秒后强杀）
set -u
cd "$(dirname "$0")/../project" || exit 1
APP="./Builds/Mac/Mistbell.app/Contents/MacOS/Mistbell"
OUT="$PWD/Shots/art_runtime.png"
LOG="$PWD/../logs/10b_runtime.log"

[ -x "$APP" ] || { echo "播放器不存在，先跑 SpikeBuild.Run"; exit 1; }
rm -f "$OUT"

"$APP" -shot "$OUT" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$LOG" &
PID=$!
for _ in $(seq 1 40); do
  [ -f "$OUT" ] && break
  sleep 1
done
sleep 2
kill "$PID" 2>/dev/null
wait "$PID" 2>/dev/null

if [ -f "$OUT" ]; then
  ls -lh "$OUT"
else
  echo "截图未生成"
fi
