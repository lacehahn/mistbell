# 任务：修好两个失败的构建步骤 + 出生点落在路上（**不做路面贴图尺度**）

> 用户裁决：① 修构建步骤　③ 出生点对齐　—— ② 路面贴图尺度不致命，本轮**不要做**。

## 1. 让 rebuild_all.sh 22/22 全绿（最高优先）
现状：
```
FarMistRocksBuild.Run  rc=1  InvalidOperationException: Sequence contains no matching element
FarMistV2Build.Run     rc=1  FormatException: Input string was not in a correct format
```
原因：这两个步骤写在"围栏改成段模型"之前，靠 `FencePost_*` 这类旧命名 `First(...)` 找对象，
名字变了就抛异常；`FarMistV2` 还有一处字符串解析失败。

要求：
- 不再用旧命名硬找：改成按材质名 / 组件 / 标签定位；**定位不到就跳过并记日志**，不许抛异常；
- 修掉 `FormatException`（加容错，日志里把出错的字符串打出来）；
- `bash spike/tools/rebuild_all.sh` 必须返回 **0**，日志里 22 步全部 rc=0。

## 2. 出生点与行进线贴合路面中心（用户指出角色站在雪地上）
- 现在 `art_runtime.png` 里角色在路的右侧雪地上，路在左边；
- 出生点、行走贴地采样、脚印生成，全部以**当前路面中心线**为准（路宽 2.1 m）：
  - 出生点：到中心线横向距离 ≤ **0.15 m**
  - 行走 6 秒后仍在中心线 ±0.25 m 内（允许轻微摆动）
- 日志 `[SPAWN] dist_to_center=…`、`[WALK_ALIGN] max_offset=…`。

## 明确不做
- **不改路面贴图的世界投影尺度**（保持 2.4 m），不改路面亮度；
- 不动 `FenceModel.shader`、人物 `_Color`、相机参数、reveal 机位、雾与山、室内、祭坛。

## 验收
- `rebuild_all.sh` rc=0（22/22）；给出最后 22 行摘要；
- `[SPAWN] dist_to_center ≤ 0.15`、`[WALK_ALIGN] max_offset ≤ 0.25`；
- 截图：`art_runtime.png`（全景，角色应在路中间）、`play_view.png`；
- 三项演示 `bad=0`；室外 `fps ≥ 200`、台地 `drawcall ≤ 330`；
- 围栏颜色与几何与当前版本一致（不许变红）。
