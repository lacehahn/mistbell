# Codex 任务：地形距离雾（地平线硬边）

## 背景（一次只说必要的事）

Mistbell 的 HD-2D spike 现在最大的「桌面模型」缺陷：**草地在地平线处一刀切到天空**。
量化基线（`python3 spike/tools/edge.py spike/project/Shots/art_runtime.png`）：

```
col x= 192 horizon= 766 Lgrnd=  85.7 Lnear=  82.9 Lsky= 170.9 jump= 84.1
...
mean jump=86.3 max=130.7 → 硬边（问题）
ground distance-lift mean=-15.2
```

`jump` = 地平线附近单行最大亮度跳变；真实照片/HD-2D 里应是几十像素的软过渡（jump < 12）。
`distance-lift` = 地平线下 3 px 与下 40 px 的亮度差，现在是 **-15**（越远越暗），说明地面**完全没有空气渗染**。

## 为什么不是「竖直雾气帘」

之前的想法是插一张竖直渐变的 alpha 帘幕。**放弃这个做法**，原因是几何压缩：
相机贴地平视、地面 4° 坡，靠地平线的地面在屏幕上被压到几十像素——帘幕平面只能涂到薄薄一条，
而且帘幕从地面「长出来」的那条交线本身就是新的硬边。距离雾必须在 45→135 m 上累积，
由透视压缩自然变成屏幕上几十像素的渐变，这只有**在着色器里按视距算**才做得到。

## 要做什么（唯一改动：地面材质换成自定义着色器）

### 1. 新建 `spike/project/Assets/Art/Shaders/TerrainHaze.shader`

- Shader 名 `Mistbell/TerrainHaze`；SubShader Tags `"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"`；Pass `LightMode="UniversalForward"`；`Cull Back`、`ZWrite On`。
- 着色模型**必须尽量接近现在的 `Universal Render Pipeline/Lit`**，否则整帧亮度基线会漂：
  - 采样 `_BaseMap` × `_BaseColor`（tiling 以米为单位，沿用现有 `_BaseMap_ST`）；
  - `_NormalMap` + `_NormalScale`（可采样，简化的切线空间法线即可；没有法线图时走 `_NORMALMAP` 关闭路径）；
  - 主光方向 Lambert（`GetMainLight()`），**接收主光阴影**（`_MAIN_LIGHT_SHADOWS` / `_MAIN_LIGHT_SHADOWS_CASCADE` multi_compile，`TransformWorldToShadowCoord` → `MainLightRealtimeShadow`）；
  - 环境光用 SH（`SampleSH(normalWS)`）；
  - 最终 `finalColor = lit * 1.0`（不要再乘 exposure/postExposure，URP 后期会做）。
- 可参考同目录已有的 `RidgeFog.shader`（URP HLSL 写法、`Core.hlsl` / `Lighting.hlsl` 的 include 方式）与 `PixelBillboard.shader`。

### 2. 距离雾（核心）

在片元里、**光照算完之后**做空气渗染（物理上正确的位置）：

```
float3 camXZ = _WorldSpaceCameraPos.xz;
float  d     = distance(worldPos.xz, camXZ);           // 只用水平距离，避免相机高度影响
float  t     = saturate((d - _HazeStart) / max(1e-3, _HazeEnd - _HazeStart));
float  f     = pow(t, _HazePower) * _HazeStrength;
color.rgb    = lerp(color.rgb, _HazeColor.rgb, f);
```

属性与**默认值**（这些值是算过的，不要凭感觉改）：

| 属性 | 默认 | 依据 |
| --- | --- | --- |
| `_HazeColor` | `#A7B7CB`（= 天空色，线性空间换算后使用） | 雾色必须等于天空色，否则交界处会换一种颜色而不是消失 |
| `_HazeStart` | 45 m | 45 m 内的近景草地一点都不能动（`art_close`/`art_ground` 的基线） |
| `_HazeEnd` | 135 m | 脊线在 125 m 左右，135 m 才到满雾，让地平线处 f≈0.85 而不是 1.0（全白会变成一块死板） |
| `_HazePower` | 1.4 | 近处慢、远处快，符合空气透视 |
| `_HazeStrength` | 0.92 | 留 8% 原色，避免远处变成纯平涂 |

`_HazeColor` 用 `Color` 属性，C# 侧直接 `SetColor` 成天空色（sRGB→linear 由 Unity 处理，不要手动 pow）。

### 3. 场景侧接线（`spike/project/Assets/Editor/SpikeScene.cs`）

- 现有 `var ground = SpikeArt.MakeTextured(...)` 生成的是 Lit 材质，**保留它**（别改 `SpikeArt.cs` 的通用逻辑）。
- 新增：以 `ground` 为模板生成 `Assets/Settings/M_GroundHaze.mat`，把 shader 换成 `Mistbell/TerrainHaze`，
  复制 `_BaseMap` 贴图与 `_BaseMap_ST`（tiling 3.2 m → 0.3125）、`_BaseColor`、`_NormalMap`，
  再设上面 5 个雾参数，然后把这个材质传给 `BuildTerrain(...)`。
- 加一行日志：`[SPIKE] terrain haze shader=TerrainHaze start=45 end=135 strength=0.92 color=#A7B7CB`。
- **不要动地形网格生成**（`TerrainHeight` / 顶点数 23103 / 脊线 `crestZ(x)`），也不要动相机、教堂、雾、远山。
- 材质是被场景引用的资产，会随场景进包；不要只在运行时 `Shader.Find` 建材质（构建后 shader 可能被剔除）。

## 验收标准（每条都必须自己跑出数字）

```bash
cd ~/workspace/mistbell
# 1) 重建场景（必须，否则用的是旧场景）
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/90_scene.log
grep -E "terrain haze|terrain mesh verts|\[SPIKE\] frame" spike/logs/90_scene.log

# 2) 出包
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/91_build.log
grep -E "Build (Succeeded|Failed)|error CS" spike/logs/91_build.log | head

# 3) 运行时三机位截图（必须 caffeinate，否则显示器休眠会静默失败）
caffeinate -dimu -t 150 bash spike/tools/capture_runtime.sh

# 4) 地平线软硬度（本轮主指标）
python3 spike/tools/edge.py spike/project/Shots/art_runtime.png

# 5) 近景与曝光不能漂
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png spike/project/Shots/art_close.png spike/project/Shots/art_ground.png
python3 spike/tools/horizon.py spike/project/Shots/art_runtime.png
```

必须同时满足：

| 指标 | 门槛 | 现状 |
| --- | --- | --- |
| `edge.py` **mean jump** | **< 25**（目标 8–18） | 86.3 |
| `edge.py` **ground distance-lift** | **> +12** | -15.2 |
| `frame.py` `art_runtime` `ground_near` | 三个通道各自与 (68, 90, 28) 相差 **≤ 6** | (68,90,28) |
| `frame.py` 三张图 `clipped>=250` | `< 1.0%` | 0.00 / 0.22 / 0.11 |
| `horizon.py` span | 129 ± 12 px（地形几何未动） | 129 |
| `[SPIKE] frame` church `heightPct` | 49.8 ± 1.0 | 49.8 |
| 双平台构建 | macOS + Windows 均 `Succeeded`，`error CS` 为 0 | 通过 |

## 报告要求

最后用清单形式给出：改了哪些文件、上面 7 条指标各自的**实测值**（贴命令输出原文），
以及有没有哪条没达标。没达标就直说没达标，不要调低门槛或改验收标准来「通过」。
