#!/usr/bin/env bash
# 室内出包的固定顺序（顺序错了地板材质会被场景重建覆盖回发白版本）：
#   1) SpikeScene.Run  重建场景（会把室内材质资产写回默认值）
#   2) TweakInteriorMats.Run  改材质资产（自发光/底色/粗糙度）
#   3) SpikeBuild.Run  出包（此时场景序列化的材质才是 tk 之后的）
# 用法: bash spike/tools/build_interior.sh [--no-capture]
set -u
cd "$(dirname "$0")/../.." || exit 1
UNITY=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
PROJ=spike/project
LOGS=spike/logs
mkdir -p "$LOGS"

run() { # $1=method $2=logname
  "$UNITY" -batchmode -quit -projectPath "$PROJ" -executeMethod "$1" -logFile "$LOGS/$2" >/dev/null 2>&1
  local rc=$?
  echo "[$1] rc=$rc log=$LOGS/$2"
  return $rc
}

run SpikeScene.Run "auto_scene.log"      || exit 1
run TweakInteriorMats.Run "auto_tweak.log" || exit 1
run SpikeBuild.Run "auto_build.log"      || exit 1

if [ "${1:-}" != "--no-capture" ]; then
  caffeinate -dimu bash spike/tools/capture_runtime.sh
fi
