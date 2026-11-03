#!/bin/bash
# 等 Codex 配额恢复后自动派发任务书（用完即退出，不是常驻服务）。
# 用法：bash spike/tools/watch_quota.sh spike/tasks/material_style_octopath.md [最长等待分钟=180] [探测间隔秒=300]
set -u
cd "$(dirname "$0")/../.." || exit 1
TASK="${1:?需要任务书路径}"
MAXMIN="${2:-180}"
GAP="${3:-300}"
DEADLINE=$(( $(date +%s) + MAXMIN * 60 ))
LOG="spike/logs/quota_watch_$(date +%H%M%S).log"
mkdir -p spike/logs

echo "[WATCH] 开始等待 Codex 配额，任务=$TASK 最长 ${MAXMIN} 分钟，每 ${GAP}s 探测一次" | tee -a "$LOG"
while [ "$(date +%s)" -lt "$DEADLINE" ]; do
  OUT=$(codex exec --sandbox read-only "只回复 PING 四个字母，不要别的" 2>&1)
  if printf '%s' "$OUT" | grep -qi "usage limit\|hit your usage"; then
    echo "[WATCH] $(date +%H:%M:%S) 配额仍未恢复" | tee -a "$LOG"
  else
    echo "[WATCH] $(date +%H:%M:%S) 配额已恢复，派发任务" | tee -a "$LOG"
    bash spike/tools/codex.sh "读 $TASK 并严格按它执行（只做这一件事；先做雪地/教堂石材/木门三个材质小样，等我确认手感再铺开）" 2>&1 | tee -a "$LOG"
    echo "[WATCH] 派发结束 rc=$?" | tee -a "$LOG"
    exit 0
  fi
  sleep "$GAP"
done
echo "[WATCH] 等待超时 ${MAXMIN} 分钟，未派发（任务书仍在 $TASK）" | tee -a "$LOG"
exit 3
