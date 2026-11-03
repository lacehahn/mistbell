#!/bin/bash
# 自保护：macOS 显示器休眠时播放器主循环会停，截图静默失败（表现为 124 超时）。
# 若不在 caffeinate 下，就自我重启一次。
if [ -z "${MISTBELL_CAFFEINATED:-}" ] && command -v caffeinate >/dev/null 2>&1; then
  MISTBELL_CAFFEINATED=1 exec caffeinate -dimu bash "$0" "$@"
fi
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
# 注意：macOS 自带 bash 3.2 在 `set -u` 下展开**空数组**会报 unbound variable，
# 所以这里把命令拼成以 APP 开头的非空数组再展开（历史 bug：GFX_ARGS[@] unbound）。
CMD=("$APP")
if [ "${MISTBELL_GFX_DIRECT:-0}" = "1" ]; then CMD+=(-force-gfx-direct); fi
CMD+=("${SHOT_ARGS[@]}")
# ⚠️ 必须用 open -n -a 启动：直接执行二进制时窗口不是 active app，Unity 不渲染，
# 演示/截图会静默卡住（日志停在启动尾部、exit 124）。open 会激活窗口。
pkill -x Mistbell 2>/dev/null   # 僵尸实例会占住窗口，让新实例卡在启动尾部
sleep 1
open -n -a "$PWD/Builds/Mac/Mistbell.app" --args "${CMD[@]:1}" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$LOG"
# open 不返回 PID：按进程名等它自己退出，最多 240 s。
for _ in $(seq 1 240); do
  pgrep -x Mistbell >/dev/null || break
  sleep 1
done
TIMED_OUT=0
if pgrep -x Mistbell >/dev/null; then
  TIMED_OUT=1
  pkill -x Mistbell 2>/dev/null
fi
RC=0
if [ "$TIMED_OUT" -eq 1 ]; then RC=124; fi

echo "LOG=$LOG"
grep -E '^\[SHOT\]|^\[SPIKE\]' "$LOG" || true
echo "--- files ---"
ls -lh "$DIR"/art_*.png 2>/dev/null || echo "无截图产出"

exit "$RC"
