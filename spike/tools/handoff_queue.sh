#!/bin/bash
# 等 Codex 配额恢复后，按顺序派发一批任务书（每轮之间等 Unity/codex 让出工程）。
# 用法: bash spike/tools/handoff_queue.sh "17:47" spike/tasks/a.md spike/tasks/b.md ...
set -u
TARGET="$1"; shift
cd "$(dirname "$0")/../.." || exit 1
mkdir -p spike/logs spike/tasks

target=$(python3 - "$TARGET" <<'PY'
import sys, datetime as dt
h, m = map(int, sys.argv[1].split(':'))
now = dt.datetime.now()
t = now.replace(hour=h, minute=m, second=0, microsecond=0)
if t <= now: t += dt.timedelta(days=1)
print(int(t.timestamp()))
PY
)
wait_s=$(( $(python3 -c "print($target)") - $(date +%s) ))
if [ "$wait_s" -lt 0 ]; then wait_s=0; fi
echo "[QUEUE] now=$(date '+%H:%M:%S') target=$TARGET sleep=${wait_s}s tasks=$*"
sleep "$wait_s"

idle() {
  for i in $(seq 1 120); do
    if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null || pgrep -f "node /opt/homebrew/bin/codex" >/dev/null; then
      echo "[QUEUE] busy at $(date '+%H:%M:%S'), wait $i"; sleep 10
    else return 0; fi
  done
  return 1
}

probe() {
  for attempt in $(seq 1 8); do
    out=$(spike/tools/codex.sh "reply with exactly: PING_OK" 2>&1)
    if echo "$out" | grep -q "PING_OK"; then return 0; fi
    echo "[QUEUE] quota probe $attempt: $(echo "$out" | tail -1 | cut -c1-120)"
    sleep 300
  done
  return 1
}

for task in "$@"; do
  name=$(basename "$task" .md)
  idle || { echo "[QUEUE] abort: project busy"; exit 3; }
  probe || { echo "[QUEUE] abort: quota still exhausted at $(date '+%H:%M:%S')"; exit 2; }
  echo "[QUEUE] dispatch $name at $(date '+%H:%M:%S')"
  spike/tools/codex.sh "$(cat "$task")" > "spike/logs/codex_${name}.log" 2>&1
  rc=$?
  echo "[QUEUE] $name exit=$rc at $(date '+%H:%M:%S')"
  sleep 20
done
echo "[QUEUE] all done at $(date '+%H:%M:%S')"
