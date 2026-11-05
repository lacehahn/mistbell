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

# Override only for failure-injection acceptance; the normal gate remains 20.
TRAIL_MIN_SPAWNS=${TRAIL_MIN_SPAWNS:-20}
if ! [[ "$TRAIL_MIN_SPAWNS" =~ ^[0-9]+$ ]]; then
  echo 'TRAIL_MIN_SPAWNS must be a non-negative integer' >&2
  exit 1
fi
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
  if [[ "$spec" == *-demo-trail* ]]; then
    TRAIL_BAD=$(python3 - "$LOG" "$TRAIL_MIN_SPAWNS" <<'PYTRAIL'
import math
import re
import sys
from pathlib import Path
text = Path(sys.argv[1]).read_text(errors='replace') if Path(sys.argv[1]).exists() else ''
minimum = int(sys.argv[2])
def terminal(tag):
    lines = re.findall(r'^\[' + tag + r'\] (.*)$', text, re.M)
    return dict(re.findall(r'(\w+)=([^\s]+)', lines[0])) if len(lines) == 1 else {}
summary, shot = terminal('TRAIL'), terminal('TRAIL_SHOT')
try:
    count = int(summary['spawns'])
    error = float(summary['ground_err_max'])
    passed = (count >= minimum and math.isfinite(error) and 0 <= error <= .02
              and int(shot['spawns']) == count and shot['walk_pass'] == 'true'
              and shot['gate'] == 'pass')
except (KeyError, ValueError):
    passed = False
print(f"[TRAIL_CHECK] min_spawns={minimum} spawns={summary.get('spawns', 'missing')} "
      f"ground_err_max={summary.get('ground_err_max', 'missing')} gate={'pass' if passed else 'FAIL'}", file=sys.stderr)
print(0 if passed else 1)
PYTRAIL
    )
    BAD=$(( ${BAD:-0} + ${TRAIL_BAD:-1} ))
  fi
  printf '%-26s rc=%s bad=%s\n' "$spec" "$RC" "$BAD"
  [ "$RC" -ne 0 ] && FAIL=1
  [ "${BAD:-0}" -gt 0 ] && FAIL=1
done
pkill -x Mistbell 2>/dev/null
echo "FAIL=$FAIL"
exit "$FAIL"
