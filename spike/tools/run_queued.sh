#!/usr/bin/env bash
# 等 Codex 配额恢复后，按顺序派发一串任务书；遇到配额耗尽就停在该任务前，继续等。
# 用法: bash spike/tools/run_queued.sh <最长等待分钟> <任务书1> [任务书2 ...]
# 特点: 一次性进程，跑完退出；不是常驻服务。
set -u
cd "$(dirname "$0")/../.." || exit 1

MAXMIN="${1:?需要最长等待分钟}"; shift
[ "$#" -ge 1 ] || { echo "需要至少一个任务书"; exit 2; }

LOG="spike/logs/queued_$(date +%H%M%S).log"
mkdir -p spike/logs
DEADLINE=$(( $(date +%s) + MAXMIN * 60 ))

say() { echo "[QUEUE] $(date +%H:%M:%S) $*" | tee -a "$LOG"; }

wait_quota() {
  while [ "$(date +%s)" -lt "$DEADLINE" ]; do
    OUT=$(codex exec --sandbox read-only "只回复 PING 四个字母" 2>&1)
    if printf '%s' "$OUT" | grep -qi "usage limit\|hit your usage"; then
      say "配额未恢复（$(printf '%s' "$OUT" | grep -oE 'try again at [^.)]*' | head -1)），5 分钟后再探"
      sleep 300
      continue
    fi
    say "配额已恢复"
    return 0
  done
  say "等待超时（${MAXMIN} 分钟），退出"
  return 1
}

for TASK in "$@"; do
  say "待派发: $TASK"
  wait_quota || exit 0
  say "开始执行: $TASK"
  OUT=$(bash spike/tools/codex.sh "读 $TASK 并严格按它执行，只做这一件事；完成后按任务书里的验收项自己跑一遍并贴数字。" 2>&1)
  printf '%s\n' "$OUT" | tee -a "$LOG" | tail -20
  if printf '%s' "$OUT" | grep -q "ERROR: You.*hit your usage limit"; then
    say "执行中配额耗尽：$TASK 未完成，退出"
    exit 0
  fi
  say "完成: $TASK"
done
say "队列清空"
