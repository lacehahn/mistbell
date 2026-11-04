#!/bin/bash
# 串行跑演示模式并汇总退出码。
# ⚠️ 必须用 open -n -a 启动：直接执行二进制时窗口不是 active app，Unity 不渲染，
#    演示会静默卡住（日志停在启动尾部、进程常驻 0% CPU）。
set -u
cd "$(dirname "$0")/../project" || exit 1
APP="$PWD/Builds/Mac/Mistbell.app"
[ -d "$APP" ] || { echo "播放器不存在，先跑 SpikeBuild.Run"; exit 1; }

# 全程压住显示器休眠：open 让进程脱离本 shell，单靠 exec caffeinate 覆盖不到播放器。
# 显示器一睡，播放器主循环就停（日志停在启动尾部 / 43 行），演示永远不推进。
caffeinate -dimu -t 1200 sleep 1200 &
CAFF=$!
trap 'kill $CAFF 2>/dev/null' EXIT

FAIL=0
for spec in "$@"; do
  pkill -x Mistbell 2>/dev/null
  sleep 1
  # open 启动时工作目录不是这里，-logFile 必须给绝对路径，否则日志写到别处、看不到结果
  LOG="$PWD/../logs/run_$(date +%H%M%S)_$(echo "$spec" | tr -d ' - ').log"
  # shellcheck disable=SC2086
  open -n -a "$APP" --args $spec -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "$LOG"
  for _ in $(seq 1 120); do
    pgrep -x Mistbell >/dev/null || break
    sleep 1
  done
  if pgrep -x Mistbell >/dev/null; then
    pkill -x Mistbell; RC=124
  else
    RC=0
  fi
  # 演示用 Application.Quit(code) 结束，进程退出码拿不到；改看日志里的失败断言。
  BAD=$(grep -acE "^\[PLAY\] .*fps_ok=false|failed|ERROR|Exception|^\[REVEAL\] gate=FAIL" "$LOG" 2>/dev/null || true)
  if [[ "$spec" == *-demo-reveal* ]] && ! grep -aqE '^\[REVEAL\] gate=(pass|FAIL) ' "$LOG"; then
    BAD=$(( ${BAD:-0} + 1 ))
    echo '[REVEAL] missing terminal gate result'
  fi
  printf '%-26s rc=%s bad=%s\n' "$spec" "$RC" "$BAD"
  [ "$RC" -ne 0 ] && FAIL=1
  [ "${BAD:-0}" -gt 0 ] && FAIL=1
done
pkill -x Mistbell 2>/dev/null
echo "FAIL=$FAIL"
exit "$FAIL"
