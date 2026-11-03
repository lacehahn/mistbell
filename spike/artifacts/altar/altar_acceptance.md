# 祭坛模型验收（2026-11-04）

已将原祭坛本体替换为真实凹龛、连续厚台布、两段倒角的 Blender 模型。三个烛台与细十字属于同一网格。旧祭坛的十字、五支蜡烛与火焰装饰移除，避免附件重叠；高壁龛、圣像屏、其他家具及所有灯光保留。

## 实测

| 指标 | 数字 | 门槛 |
|---|---:|---:|
| 三角面 | 8,468 | ≤9,000 |
| 材质槽 | 2 | ≤2 |
| 本体包围盒，不含十字和烛台 | 2.2400 × 1.0500 × 1.0102 m | 2.20±0.10 × 1.05±0.08 × 0.95±0.10 m |
| 实际 UV 面积推算密度 | 127.741 px/m | 110–150 |
| Unity 三处龛背板射线深度 | 0.0500 / 0.0500 / 0.0500 m | 0.05 m |
| 室内平均帧率 | 233.29 FPS | ≥200 |
| 室内 drawcalls | 236 | ≤260 |
| 室内 1% low（附录，不作平均帧率承诺） | 166.41 FPS | 本任务无此门槛 |

密度采用 `1024 × sqrt(ΣUV三角形面积 / Σ世界空间三角形面积)`，分别在 Blender 和 Unity 导入网格上测量。Unity 射线从会众侧朝 +Z，比较正面石材与三块背板的命中点，验证导入方向与真实凹陷。

`-demo-enter-church`：`light=True domeLight=True ceilingSeen=True domeSeen=True visible=True obstacles=True walked=True`；FOV=50，lamps=18，shafts=3。未修改演示判定或阈值。原 `Interior_Altar` 仍为碰撞测试对象；原场景实际为 MeshCollider，转换为相同世界包围盒的 BoxCollider（2.20 × 1.00 × 0.90 m）。模型原点仍为 (-2, FloorY, 8.6)，龛面朝 -Z。

性能使用既有开发构建与 `-bench -bench-foreground`，1920×1080、Metal / Apple M6；每机位至少 180 帧且至少 3 秒预热，采样 300 帧，vSync=0。完整原始结果见 `../../logs/altar_bench.json`。

## 产物与截图

- 模型：`../../export/gergeti_altar.fbx`；源文件 `../../export/gergeti_altar.blend`；脚本 `../../tools/build_altar.py`。
- 图集：`../../art-prompts/staged/altar_stone.png`，1024×1024，石材 / 暖木金饰 / 亚麻 / 亮石材四象限，单张图集。龛暗部材质乘 0.82，仍共用该图集。
- 通过内置 image_gen 生成，参考 `stone_big_a.png`、`stone_cornice.png` 的色板与笔触。完整提示词见 `../../art-prompts/prompt_altar_stone.txt`。经现有 `ingest_handpaint.py --altar-only` 导入 `Assets/Art/MaterialForge/Altar_Painted.png`，不写室外图集。
- 近景：`../../project/Shots/altar_closeup.png`。镜头距正面石材 1.2 m，朝 +Z。**这是隔离检查图**：原有高壁龛会遮住十字上半部，因此仅此演示进程的独立相机显示祭坛，保留调色和色调映射、仅关闭模糊，源网格从运行时合批中单独显示；使用现有场景光源。没有改变玩法相机或保存其他物件的可见性。
- 原机位：`../../project/Shots/interior_inside.png`，由未修改判定的入堂演示生成，保留原相机、后期、家具与遮挡。

目视检查：三龛凹陷可辨、倒角与框条有明暗差、连续台布有三道立体褶、十字与三烛台采样暖木金饰纹理。128 px/m 的像素颗粒为设计密度；正常机位保留原景深与 HD-2D 后处理。

## 复现

在仓库根目录执行：

```sh
/opt/homebrew/bin/blender -b --python-exit-code 1 -P spike/tools/build_altar.py
python3 spike/tools/ingest_handpaint.py --altar-only
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project -executeMethod AltarBuild.Run -logFile spike/logs/altar_unity_build.log
open -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -demo-altar -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/altar_closeup_runtime.log"
```

等播放器退出，再分别运行 `-demo-enter-church` 与 `-bench -bench-foreground -bench-output <绝对输出路径>`，不要并行运行播放器。`AltarBuild.Run` 仅打开既有场景更新祭坛，验证导入网格后调用既有 `PerformanceBuild.Run`；全场景重建也通过 `ChurchInteriorBuilder.InstallAltar` 使用同一实现。

日志：`../../logs/altar_model.log`、`altar_ingest.log`、`altar_unity_build.log`、`altar_inspection_build.log`、`altar_closeup_runtime.log`、`altar_interior_runtime.log`、`altar_bench_runtime.log`。Python 编译检查、`git diff --check` 通过。生成的 Unity 工程资产、FBX、BLEND、构建与日志按仓库现有 `.gitignore` 保留本地，未提交。
