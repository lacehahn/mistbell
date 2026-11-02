# Mistbell（雾钟）

HD-2D 风格的「朝圣」探索 RPG。首个地点：格鲁吉亚卡兹别克 · **Gergeti 圣三一教堂**。
玩法：从山脚向上攀爬 → 景色渐变 → 云雾中显现教堂 → 对景点交互并弹出介绍。

## 当前阶段

**美术管线 spike**（M1 之前）。已打通一条完整闭环：Blender 建模 → FBX 导入 Unity → 运行时截图 → 量化验收。

| 环节 | 状态 |
| --- | --- |
| 教堂模型 | 28.17 × 23.05 × 24.50 m（对齐设定画 28×22×24.5），73,674 tris / 2 个 submesh |
| 场景 | 雪山、三层远山、薄雪地面、斜射暖光+实时阴影、雾、火盆、窗内暖光 |
| 主角 | 24 帧像素动画（4 方向 × 6 帧），图集 384×320，10 fps |
| 验收 | `spike/tools/measure_shot.py` 量化：雪覆盖 / 过曝 / 暗斑 / 投影 / 颗粒能量 / 暖光 |

## 目录

```
docs/        设计文档（愿景、美术方向、玩法、流程、技术选型、MVP、决策表）
art-ref/     设定画与美术规格（教堂设定画、主角三视图与行走表、规格 md）
spike/
  Blend/     Blender 建模脚本与 .blend（三视图输出在 blend/views/）
  project/   Unity 工程（Assets/Editor 场景生成、Assets/Art 美术资产）
  tools/     codex.sh / build_church.py / capture_runtime.sh / measure_shot.py
  PROGRESS.md 推进日志（每步含命令与实测数字）
```

## 环境

- Unity **6000.3.27f2**（LTS）+ URP **17.0.1**
- Blender **5.2.3 LTS**
- macOS（Apple Silicon），Windows 整包验证推迟到开发末期

## 常用命令

```bash
# 重生成教堂（Blender）
blender -b --python spike/tools/build_church.py

# 生成场景 / 出包 / 运行时截图
UNITY=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
$UNITY -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/scene.log
$UNITY -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/build.log
caffeinate -dimu bash spike/tools/capture_runtime.sh   # 必须套 caffeinate，否则休眠时播放器不推进

# 量化验收当前帧
python3 spike/tools/measure_shot.py spike/project/Shots/art_runtime.png
```

## 注意

- **FBX 不入库**（可由脚本重生成）；`.blend` 与正交三视图入库。
- 运行时截图必须走播放器（`-shot`），编辑态 `cam.Render()` 不跑 Volume 后处理。
- `Library/`、`Builds/`、`Textures/`、`scratch/` 均在 `.gitignore`。

设计文档入口：[`docs/README.md`](docs/README.md) ｜ 推进日志：[`spike/PROGRESS.md`](spike/PROGRESS.md)
