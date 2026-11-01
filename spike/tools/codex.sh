#!/bin/bash
# Mistbell 统一 Codex 调用入口：固定 gpt-6.1-sol + medium 推理强度（2026-11-02 用户指定）
# 用法：spike/tools/codex.sh "读 spike/brief-codex.md 的 D1 段并执行"
set -u
cd "$(dirname "$0")/../.." || exit 1   # → 项目根
exec codex exec --sandbox danger-full-access -m gpt-6.1-sol -c model_reasoning_effort="medium" "$@"
