# 祭坛像素圣像验收（2026-11-04）

三块龛内画面已改成原生逐像素绘制的圣母子、受难十字、持杖圣徒。自动数值与运行门槛通过；已目视检查场景近景、原机位与 480 缩图，近景三种轮廓可辨。像素颗粒是否符合最终美术标准，保留截图供负责人复核。

## 交付与实现

- [左](../../project/Assets/Art/Interior/Altar_Icons/icon_left.png)、[中](../../project/Assets/Art/Interior/Altar_Icons/icon_mid.png)、[右](../../project/Assets/Art/Interior/Altar_Icons/icon_right.png)：RGB、512×784，原生 72×110 逻辑像素，PIL 整数坐标矩形、折线、多边形与椭圆绘制，无抗锯齿、渐变、照片或渲染图输入。对应 `_logical.png` 是可复核的逻辑原稿。
- [生成与像素验证](../../tools/altar_icons_pixel.py)、[Unity 接入](../../project/Assets/Editor/AltarPixelIcons.cs)、[汇总验证](../../tools/verify_altar_icons.py)。无新增依赖。
- `altar_icons_atlas.png` 为 2048×2048：底部左侧 1024×1024 保留旧 `Altar_Painted.png` 的每个原始像素；顶部三块独立 512×784 区域。原有非圣像 UV 减半，保持原采样密度；仅画面 UV 映射至三个独立区域。既有 FBX、Blender 与所有几何顶点/索引不变。
- `altar_icons_emission.png` 在三个画面外全黑，复用凹槽材质 `_EmissionMap`、`_EmissionColor=0.7`；修正原材质 `_EMISSION` 实际未启用导致的左右画面过暗。仅三个画面得到可读性补偿，未调整场景灯光。
- 全部交付 PNG、图集与遮罩导入：Point、mipmap=false、aniso=1、Uncompressed、NPOT=None；三个交付 PNG 的 Unity 实际尺寸仍为 512×784。
- 仍用原两个材质槽。原主材质着色器增加 `_AltarUVScale=2`，使旧台布区判定在图集扩容后保持原效果。`ChurchInteriorBuilder.InstallAltar` 在已有图集时自动应用此 UV 更新，后续重建不会退回旧画面。
- `AltarBuild.Verify` 的一般材质密度统计改为实际图集尺寸，排除原生高密度圣像；石材/台布密度 **126.136 px/m**，原 110–150 门槛不变；圣像通过单独的网格检查。

## 规格冲突及处理

1. **72×110 不能整数倍放大成 512×784。** 采用任务指定最终尺寸的最近邻有理映射，单格为 7 或 8 texel；网格边界相对理想坐标的最大偏差 x=0.4445、y=0.4910 px，低于任务 ±1 px 容差。没有声称存在整数缩放倍数。Python 验证逐字节重建相等，Unity 另对每个导入像素与逻辑原稿的最近邻映射逐一比较。
2. 指定深褐 `#2A1E16`、木褐 `#4A3526` 及米白 `#E8DFCF` 不能同时满足逐像素 L*25–70。保留其色系，使用 `#4E3A2D`、`#594332`、`#ACA393` 等限明度变体；石灰、金、赭红、冷灰沿用任务原值。完整色板见 [像素报告](altar_icons_pixels.json)，未把色板变体写成原始精确色值。
3. 原模型含金边外尺寸为 **0.30×0.46 m**，内画面实际为 **0.26×0.42 m**。遵守“不动模型几何/金边”，保持这两组原尺寸及位置。

## 数值与运行证据

| 项目 | 左 | 中 | 右 |
|---|---:|---:|---:|
| 唯一颜色数 | 8 | 8 | 10 |
| 逐像素 L* 最小/最大（sRGB → CIELAB D65） | 26.260 / 68.318 | 26.260 / 68.318 | 26.260 / 68.318 |
| 金色面积 | 5.023% | 2.631% | 1.432% |
| 网格、导入配置 | 通过 | 通过 | 通过 |

L* 为交付纹理的测量值，场景截图仍受原光照与后期影响。Python 与 Unity 分别统计颜色/明度并输出 `[ALTAR_ICONS]`，Unity 同时验证导入后逻辑网格。

- 构建：[altar_icons_build.log](../../logs/altar_icons_build.log)，`Succeeded errors=0`。
- 几何与幂等重应用：[altar_icons_audit.log](../../logs/altar_icons_audit.log)，三块各 24 顶点，原顶点与各子网格三角形索引逐一相等；**8880 tris / 2 materials**；`[ALTAR_MODEL] pass=True`。未修改原有祭坛/图标墙碰撞、室外、烛台、台布或十字几何。原有 v3 十字遮挡审计仍保留，不将其 `visible_from_cam=false` 冒充本任务新增失败或修复。
- 入堂：[altar_icons_interior.log](../../logs/altar_icons_interior.log)，`light=True domeLight=True ceilingSeen=True domeSeen=True visible=True obstacles=True walked=True`。演示断言、路线和原机位未改。
- 近景：[altar_icons_closeup.log](../../logs/altar_icons_closeup.log)，距祭坛正面 **1.200 m**，非隔离检查；沿用既有近景仅关模糊的配置。
- 性能：[altar_icons_bench.json](../../logs/altar_icons_bench.json)、[原始日志](../../logs/altar_icons_bench.log)：室内 **237 drawcalls / 233.36 平均 FPS / 166.37 FPS 1% low**，通过 ≤260 / ≥200 门槛。环境 Apple M6 / Metal，1920×1080，开发播放器，vSync=0，至少 180 帧且 3 秒预热，300 帧采样。室外机位为 259/278 drawcalls，任务的 260 门槛按祭坛所在室内机位判定；完整三机位数据保留。
- 汇总：[altar_icons_acceptance.json](altar_icons_acceptance.json)，包含原截图 SHA-256。生成物与 PNG 沿用项目忽略规则保留本地，未提交。已有工作区改动保留，未修改 AGENTS.md。

## 截图

- [altar_closeup.png](../../project/Shots/altar_closeup.png)：本轮场景近景，1920×1080。
- [interior_inside.png](../../project/Shots/interior_inside.png)：本轮原机位，保留景深与遮挡，远处小画面不作为细节辨认镜头。
- [altar_icons_480.png](../../project/Shots/altar_icons_480.png)：近景经 LANCZOS 缩至 480×270，再 NEAREST 放大 4 倍至 1920×1080。三种轮廓、背景与姿态可辨；没有裁剪、提亮或替换源截图内容。

## 复现

仓库根执行，Unity 与播放器分别串行运行：

```sh
python3 spike/tools/altar_icons_pixel.py
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project -executeMethod AltarPixelIcons.Run -logFile "$PWD/spike/logs/altar_icons_build.log"
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project -executeMethod AltarPixelIcons.Audit -logFile "$PWD/spike/logs/altar_icons_audit.log"
caffeinate -dimu -t 300 open -W -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -demo-altar -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/altar_icons_closeup.log"
caffeinate -dimu -t 300 open -W -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -demo-enter-church -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/altar_icons_interior.log"
caffeinate -dimu -t 300 open -W -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -bench -bench-foreground -bench-output "$PWD/spike/logs/altar_icons_bench.json" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/altar_icons_bench.log"
python3 spike/tools/verify_altar_icons.py
git diff --check
```

最后的 Python 命令会拒绝构建前旧截图、未通过的演示/性能、错误色板/网格，并重新生成 480 缩图。
