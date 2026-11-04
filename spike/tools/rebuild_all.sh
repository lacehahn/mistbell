#!/usr/bin/env bash
# 全量重建：按"功能引入的先后顺序"依次执行所有 Editor 构建步骤。
#
# 为什么需要这个脚本：每轮任务各自加了一个 *Build.cs，只跑 SpikeScene.Run + SpikeBuild.Run
# 会把之前的功能全部丢掉（路恢复成旧砖、围栏回到方块）。这份顺序来自 git 提交历史。
#
# 用法: bash spike/tools/rebuild_all.sh [--no-build]
set -u
cd "$(dirname "$0")/../.." || exit 1
UNITY=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
PROJ=spike/project
LOGS=spike/logs/rebuild
mkdir -p "$LOGS"

STEPS=(
  "SpikeScene.Run:01_scene"                # 基础场景（教堂/地形/相机/演示对象）
  "MaterialForge.Generate:02_materials"    # 程序化/手绘材质导入
  "TweakInteriorMats.Run:03_tweak_interior_mats"
  "AltarBuild.Run:04_altar_model"
  "RoyalDoorBuild.Run:05_royal_door"
  "InteriorBalanceBuild.Run:06_int_balance"
  "InteriorPewsBuild.Run:07_int_pews"
  "InteriorWindowsBuild.Run:08_int_windows"
  "InteriorMaterialsBuild.Run:09_int_materials"
  "InteriorToneBuild.Run:10_int_tone"
  "InteriorPilastersDomeBuild.Run:11_int_pilasters"
  "PilgrimHeightBuild.Run:12_pilgrim"
  "CameraDistanceBuild.Run:13_camera"
  "FarMistRocksBuild.Run:14_far_mist"
  "FarMistV2Build.Run:15_far_mist2"
  "SnowGrassFenceBuild.Run:16_snow_grass_fence"
  "PathTextureBuild.Run:17_path_texture"
  "FencePathToneBuild.Run:18_fence_path_tone"
  "FenceModelBuild.Run:19_fence_model"
  "PathWidthBendBuild.Run:20_path_width"
  "FenceRefineBuild.Run:21_fence_refine"
  "PerformanceBuild.Run:22_performance"
)

fail=0
for s in "${STEPS[@]}"; do
  method="${s%%:*}"; tag="${s##*:}"
  "$UNITY" -batchmode -quit -projectPath "$PROJ" -executeMethod "$method" -logFile "$LOGS/$tag.log" >/dev/null 2>&1
  rc=$?
  errs=$(grep -c "error CS" "$LOGS/$tag.log" 2>/dev/null || echo 0)
  echo "[REBUILD] $method rc=$rc cs_errors=$errs log=$LOGS/$tag.log"
  if [ "$rc" -ne 0 ] || [ "$errs" != "0" ]; then fail=1; fi
done

if [ "${1:-}" != "--no-build" ]; then
  "$UNITY" -batchmode -quit -projectPath "$PROJ" -executeMethod SpikeBuild.Run -logFile "$LOGS/23_player.log" >/dev/null 2>&1
  echo "[REBUILD] SpikeBuild.Run rc=$?"
fi

echo "[REBUILD] done fail=$fail"
exit $fail
