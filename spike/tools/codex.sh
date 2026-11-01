#!/bin/bash
# Mistbell 统一 Codex 调用入口：固定 gpt-6.1-sol + medium 推理强度（2026-11-02 用户指定）
# 用法：spike/tools/codex.sh "读 spike/brief-codex.md 的 D1 段并执行"
#
# 并发锁（2026-11-02 09:00 加）：本仓库会被多个 cron 会话同时操作，同一时刻只允许一个
# Codex 写工作区。锁是 mkdir 原子操作，持有者写入 spike/.codexlock/OWNER。
#   - 抢不到：等待最多 $WAIT 秒后放弃（不排队写文件，避免互相覆盖）
#   - 持有超过 $STALE 分钟视为僵死（会话被杀），下一个调用者可强制接管
#   - 确实需要并行/接管：CODEX_NO_LOCK=1 spike/tools/codex.sh "..."
set -u
cd "$(dirname "$0")/../.." || exit 1   # → 项目根

LOCK="$PWD/spike/.codexlock"
STALE_MIN=${CODEX_LOCK_STALE_MIN:-45}
WAIT=${CODEX_LOCK_WAIT:-60}
OWNER="$$@$(hostname -s) $(date '+%H:%M:%S')"

if [ "${CODEX_NO_LOCK:-0}" != "1" ]; then
  deadline=$(( $(date +%s) + WAIT ))
  while :; do
    if mkdir "$LOCK" 2>/dev/null; then
      echo "$OWNER" > "$LOCK/OWNER"
      trap 'rm -rf "$LOCK"' EXIT INT TERM
      echo "[CODEX] lock acquired: $LOCK ($OWNER)"
      break
    fi
    age=$(( ( $(date +%s) - $(stat -f %m "$LOCK" 2>/dev/null || echo 0) ) / 60 ))
    if [ "$age" -ge "$STALE_MIN" ]; then
      echo "[CODEX] stale lock ${age}m >= ${STALE_MIN}m, taking over: $(cat "$LOCK/OWNER" 2>/dev/null)"
      rm -rf "$LOCK"
      continue
    fi
    if [ "$(date +%s)" -ge "$deadline" ]; then
      echo "[CODEX] ABORT: another Codex holds the repo lock ${age}m: $(cat "$LOCK/OWNER" 2>/dev/null)" >&2
      echo "[CODEX] 不要并行写同一工作区；等它结束，或 CODEX_NO_LOCK=1 强制接管" >&2
      exit 3
    fi
    echo "[CODEX] waiting for lock: $(cat "$LOCK/OWNER" 2>/dev/null) (${age}m)"
    sleep 10
  done
fi

exec codex exec --sandbox danger-full-access -m gpt-6.1-sol -c model_reasoning_effort="medium" "$@"
