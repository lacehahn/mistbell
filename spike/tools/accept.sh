#!/bin/bash
# Mistbell 一键验收：建场景 → 出包 → 11 项演示 → 三张截图 → 量测 → 满分辨率性能。
# 用法：
#   bash spike/tools/accept.sh            # 全跑
#   bash spike/tools/accept.sh --fast     # 跳过 Windows/性能，只跑演示与截图
# 退出码 0 = 全绿；非 0 = 有项目失败（明细在 spike/logs/accept_<时间>.log）。
set -u
cd "$(dirname "$0")/../.." || exit 1
PROJ="$PWD/spike/project"
UNITY="/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity"
LOGDIR="$PWD/spike/logs"
STAMP=$(date +%H%M%S)
LOG="$LOGDIR/accept_$STAMP.log"
FAST=0; [ "${1:-}" = "--fast" ] && FAST=1
mkdir -p "$LOGDIR"
exec > >(tee -a "$LOG") 2>&1

FAIL=0
step(){ printf '\n=== %s ===\n' "$1"; }
check(){ if [ "$1" -eq 0 ]; then printf '  ✓ %s\n' "$2"; else printf '  ✗ %s (rc=%s)\n' "$2" "$1"; FAIL=$((FAIL+1)); fi }

# 全程压住显示器休眠：播放器一旦被"窗口不可见"或屏幕休眠卡住，演示会静默不动。
caffeinate -dimu -t 3600 sleep 3600 &
CAFF=$!
trap 'kill $CAFF 2>/dev/null; pkill -x Mistbell 2>/dev/null' EXIT

# Keep the task-specified canonical source in sync with Unity's supported compile path.
if ! cmp -s spike/editor/MaterialForge.cs "$PROJ/Assets/Editor/MaterialForge.cs"; then
  cp spike/editor/MaterialForge.cs "$PROJ/Assets/Editor/MaterialForge.cs"
fi
step "1/5 生成场景"
python3 spike/tools/forge_style10.py
check $? "STYLE10 生成（冻结四张参考贴图）"
"$UNITY" -batchmode -quit -projectPath "$PROJ" -executeMethod SpikeScene.Run -logFile "$LOGDIR/accept_scene.log" >/dev/null 2>&1
check $? "SpikeScene.Run"
grep -aoE "\[SNOW\][^\"]{0,80}" "$LOGDIR/accept_scene.log" | tail -1
grep -aoE "\[FGD_LAYOUT\][^\"]{0,120}" "$LOGDIR/accept_scene.log" | tail -1

step "2/5 出包（macOS + Windows Mono）"
"$UNITY" -batchmode -quit -projectPath "$PROJ" -executeMethod SpikeBuild.Run -logFile "$LOGDIR/accept_build.log" >/dev/null 2>&1
check $? "SpikeBuild.Run"
grep -c "Build Finished, Result: Success" "$LOGDIR/accept_build.log" | xargs -I{} echo "  build success × {}"

step "3/5 演示（11 项）"
bash spike/tools/run_demos.sh \
  "-demo-walk 6" "-demo-walk-stairs" "-demo-walk-to-tower" "-demo-enter-church" \
  "-demo-cinema" "-demo-reveal" "-demo-journal" "-demo-settings" "-demo-save" "-demo-load" "-demo-trail 10" \
  > "$LOGDIR/accept_demos_$STAMP.log" 2>&1
check $? "11 项演示"
tail -12 "$LOGDIR/accept_demos_$STAMP.log"
grep -ahoE "\[(REVEAL|JOURNAL_CHECK|SAVE)\] [^\"]{0,110}" "$LOGDIR"/run_*_"$STAMP"*.log 2>/dev/null | tail -3

step "4/5 截图 + 量测"
CAPTURE_START=$(date +%s)
bash spike/tools/capture_style10.sh > "$LOGDIR/accept_capture_$STAMP.log" 2>&1
check $? "capture_style10.sh（本轮外景、室内与480放大图）"
python3 spike/tools/measure_style10.py
check $? "STYLE10 布局 / 渲染色调 / SHA / 整帧门槛"

if [ "$FAST" -eq 0 ]; then
  step "5/5 满分辨率性能（1920×1080）"
  pkill -x Mistbell 2>/dev/null; sleep 1
  open -n -a "$PROJ/Builds/Mac/Mistbell.app" --args -bench -bench-foreground -screen-width 1920 -screen-height 1080 \
    -screen-fullscreen 0 -logFile "$LOGDIR/accept_bench_$STAMP.log"
  for _ in $(seq 1 240); do pgrep -x Mistbell >/dev/null || break; sleep 1; done
  pkill -x Mistbell 2>/dev/null
  grep -aoE "\[BENCH\] pos=[^\"]{0,90}" "$LOGDIR/accept_bench_$STAMP.log" | tail -3
  grep -aq "BENCH_GATE] pass=True" "$LOGDIR/accept_bench_$STAMP.log"
  check $? "BENCH_GATE pass"
else
  step "5/5 性能已跳过（--fast）"
fi

printf '\n================ 结果 ================\n'
if [ "$FAIL" -eq 0 ]; then
  echo "全绿 ✓  日志：$LOG  截图：spike/project/Shots/"
else
  echo "失败项：$FAIL  日志：$LOG"
fi
exit "$FAIL"
