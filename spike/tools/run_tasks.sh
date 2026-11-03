#!/bin/bash
# 顺序执行一批任务书（不等待时间，只等工程空闲）。每轮之间检查 Unity/codex 是否退出。
# 用法: bash spike/tools/run_tasks.sh spike/tasks/a.md spike/tasks/b.md ...
set -u
cd "$(dirname "$0")/../.." || exit 1
mkdir -p spike/logs

idle() {
  for i in $(seq 1 180); do
    if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null || pgrep -f "node /opt/homebrew/bin/codex" >/dev/null; then
      echo "[RUN] busy ($i) at $(date '+%H:%M:%S')"; sleep 10
    else return 0; fi
  done
  return 1
}

for task in "$@"; do
  name=$(basename "$task" .md)
  idle || { echo "[RUN] abort: project busy"; exit 3; }
  echo "[RUN] start $name at $(date '+%H:%M:%S')"
  spike/tools/codex.sh "$(cat "$task")" > "spike/logs/codex_${name}.log" 2>&1
  rc=$?
  echo "[RUN] done $name exit=$rc at $(date '+%H:%M:%S')"
  if [ "$rc" -ne 0 ]; then
    # 配额耗尽就停，别空转
    if tail -20 "spike/logs/codex_${name}.log" | grep -q "usage limit"; then
      echo "[RUN] quota exhausted at $(date '+%H:%M:%S')"; exit 2
    fi
  fi
  sleep 15
done
echo "[RUN] all done at $(date '+%H:%M:%S')"
