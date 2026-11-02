#!/bin/bash
# 等到 Codex 配额窗口重置后，自动把"山体"这项交给 Codex。
# 用法: bash spike/tools/handoff_mtn.sh "12:46"
set -u
TARGET="${1:-12:46}"
cd "$(dirname "$0")/../.." || exit 1

now=$(date +%s)
# macOS 不支持 date -d，用 python 算下一次目标时刻
target=$(python3 - "$TARGET" <<'PY'
import sys, datetime as dt
h, m = map(int, sys.argv[1].split(':'))
now = dt.datetime.now()
t = now.replace(hour=h, minute=m, second=0, microsecond=0)
if t <= now: t += dt.timedelta(days=1)
print(int(t.timestamp()))
PY
)
wait_s=$(( target - now ))
echo "[HANDOFF] now=$(date '+%H:%M:%S') target=$TARGET sleep=${wait_s}s"
[ "$wait_s" -gt 0 ] && sleep "$wait_s"

# 1) 等 Unity / 其他 codex 让出工程（最多 20 分钟）
for i in $(seq 1 120); do
  if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null || pgrep -f "codex exec" >/dev/null; then
    echo "[HANDOFF] busy (unity/codex), retry $i at $(date '+%H:%M:%S')"; sleep 10
  else break; fi
done

# 2) 探测配额是否恢复（最多重试 6 次，每次 5 分钟）
for attempt in $(seq 1 6); do
  echo "[HANDOFF] quota probe $attempt at $(date '+%H:%M:%S')"
  out=$(spike/tools/codex.sh "reply with exactly: PING_OK" 2>&1)
  if echo "$out" | grep -q "PING_OK"; then
    echo "[HANDOFF] quota OK, dispatch"
    spike/tools/codex.sh "$(cat spike/tasks/mountain_triplanar.md)" > spike/logs/codex_mtn_handoff.log 2>&1
    echo "[HANDOFF] codex exit=$? at $(date '+%H:%M:%S')"
    exit 0
  fi
  echo "$out" | tail -1
  sleep 300
done
echo "[HANDOFF] quota still exhausted after 6 probes at $(date '+%H:%M:%S')"
exit 2
