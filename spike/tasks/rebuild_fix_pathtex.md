# 任务：修好两个失败的构建步骤 + 路面贴图尺度 + 出生点落在路上

## 背景
为了修复"场景重建会吞掉功能"的问题，我加了 `spike/tools/rebuild_all.sh`（按提交顺序跑 22 个 Editor 步骤）。
全量跑通 20/22，两个远景步骤失败，导致它们在 `rebuild_all` 里永远是红的：

```
FarMistRocksBuild.Run  rc=1  InvalidOperationException: Sequence contains no matching element
FarMistV2Build.Run     rc=1  FormatException: Input string was not in a correct format
```

原因：这两个步骤是**围栏改模型之前**写的，靠 `FencePost_*` 这类旧命名去找对象，现在围栏已经是
"A/B/C 段模型 + 共享柱"，名字变了所以 `First(...)` 抛异常、解析也失败。

## 要做的

### 1. 让这两个步骤与新围栏共存（优先级最高）
- 不再依赖 `FencePost_*` 命名：改用**按组件/标签/材质**定位（例如 `r.sharedMaterial.name=="M_FenceWood"`），
  或者定位不到时**跳过该项并记日志**，不要抛异常；
- `FarMistV2Build` 的 `FormatException` 修掉（定位是哪一行解析字符串，加容错）；
- 要求：`bash spike/tools/rebuild_all.sh` 返回 **0**，22 步全绿。

### 2. 路面贴图尺度（上一轮没做成）
- 世界投影 **2.4 m → 1.35 m**，`_BaseColor` 提亮 **+6%**（目标近景 `L* 58–62`）；
- **不要动** `FenceModel.shader`、人物 `_Color`、相机、截图机位（上一轮就是在这里翻车：围栏变红、机位变形）。

### 3. 出生点 / 行进线要在路中央
- 现在 `art_runtime.png` 里角色站在雪的右侧、路在左边，明显不在路上；
- 要求：**出生点、行走贴地采样、脚印生成**都以**当前路面中心线**为准（路宽 2.1 m，中心 ±0.3 m 内）；
- 日志 `[SPAWN] dist_to_path_center=≤0.15`。

## 验收
- `rebuild_all.sh` 退出码 0，且 `22/22 pass`；
- `[PATH_TEX2] scale_m=1.35 luma=58–62`；
- `[SPAWN] dist_to_path_center≤0.15`；
- 截图：`play_view.png`（玩法机位）、`art_runtime.png`（全景）、`path_closeup.png`（路面近景，能读出一块块石板）；
- 三项演示 `bad=0`；室外 `fps ≥ 200`、台地 `drawcall ≤ 330`；
- 围栏颜色/几何与上一版**完全一致**（不许再变红）。

## 硬约束
- 不动室内、祭坛、人物身高、相机参数、reveal 机位、雾与山的观感。
- 完成后把 `rebuild_all.sh` 作为标准重建入口写进 `spike/PROGRESS.md`。
