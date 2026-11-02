#!/bin/bash
# Mistbell 统一 Codex 调用入口：固定 gpt-6.1-sol + medium 推理强度（2026-11-02 用户指定）
# 用法：spike/tools/codex.sh "读 spike/brief-codex.md 的 D1 段并执行"
#
# 并发锁（2026-11-02 09:00 加，09:07 加 PID 存活判定）：本仓库会被多个 cron 会话同时操作，
# 同一时刻只允许一个 Codex 写工作区。锁是 mkdir 原子操作：
#   - `spike/.codexlock/OWNER` 第一行是 <pid> <host> <time>
#   - 抢占判定顺序：① 时间超过 $STALE_MIN 分钟（僵死上限）② 同机 PID 已不存在（进程被杀时
#     trap 可能没机会执行 → 这条是最常见的真实情况）③ 等待 $WAIT 秒仍拿不到 → exit 3 干净失败
#   - 确实需要并行/接管：CODEX_NO_LOCK=1 spike/tools/codex.sh "..."
set -u
cd "$(dirname "$0")/../.." || exit 1   # → 项目根

LOCK="$PWD/spike/.codexlock"
STALE_MIN=${CODEX_LOCK_STALE_MIN:-45}
WAIT=${CODEX_LOCK_WAIT:-60}
HOST=$(hostname -s)

holder_pid() { head -1 "$LOCK/OWNER" 2>/dev/null | awk '{print $1}' | cut -d@ -f1; }
holder_desc() { cat "$LOCK/OWNER" 2>/dev/null | tr '\n' ' '; }

takeover() {
  echo "[CODEX] $1, taking over: $(holder_desc)"
  rm -rf "$LOCK"
}

if [ "${CODEX_NO_LOCK:-0}" != "1" ]; then
  deadline=$(( $(date +%s) + WAIT ))
  while :; do
    if mkdir "$LOCK" 2>/dev/null; then
      printf '%s %s %s\n' "$$" "$HOST" "$(date '+%H:%M:%S')" > "$LOCK/OWNER"
      trap 'rm -rf "$LOCK"' EXIT INT TERM
      echo "[CODEX] lock acquired: $LOCK ($$@$HOST)"
      break
    fi
    age=$(( ( $(date +%s) - $(stat -f %m "$LOCK" 2>/dev/null || echo 0) ) / 60 ))
    pid=$(holder_pid)
    # 同机持有者已不存在 → 立即接管（进程被 kill 时 trap 常常来不及跑）
    if [ -n "$pid" ] && case "$pid" in ''|*[!0-9]*) false;; *) true;; esac \
       && grep -q "$HOST" "$LOCK/OWNER" 2>/dev/null && ! kill -0 "$pid" 2>/dev/null; then
      takeover "holder pid $pid is gone"
      continue
    fi
    if [ "$age" -ge "$STALE_MIN" ]; then
      takeover "stale lock ${age}m >= ${STALE_MIN}m"
      continue
    fi
    if [ "$(date +%s)" -ge "$deadline" ]; then
      echo "[CODEX] ABORT: another Codex holds the repo lock ${age}m: $(holder_desc)" >&2
      echo "[CODEX] 不要并行写同一工作区；等它结束，或 CODEX_NO_LOCK=1 强制接管" >&2
      exit 3
    fi
    echo "[CODEX] waiting for lock: $(holder_desc) (${age}m)"
    sleep 10
  done
fi

# 不能用 exec：exec 会替换掉本 shell，EXIT trap 再也不会执行 → 锁永远残留（2026-11-02 09:55 实证）
codex exec --sandbox danger-full-access -m gpt-6.1-sol -c model_reasoning_effort="medium" "$@"
rc=$?
exit $rc
