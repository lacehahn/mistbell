# Codex 任务：带状移轴（tilt-shift）——当前最缺的 HD-2D 特征

## 背景（只给必要信息）

Mistbell 的 HD-2D spike 画面已经解决：构图、曝光、像素材质、远山分层、地平线距离雾。
**现在最缺的是《歧路旅人》最标志性的一层：上下虚化的带状移轴**。URP 自带的 Bokeh DoF 是
体积式（按深度），做不出「上下两条带模糊、中间一条带清晰」的假微缩感。

现有基线（`python3 spike/tools/sharp.py`，高频能量 = 邻像素亮度差均值）：

```
art_runtime.png  hf  top=  0.00  middle=  4.58  bottom=  7.76
art_close.png    hf  top= 13.87  middle= 15.76  bottom= 12.03
art_ground.png   hf  top=  8.42  middle= 12.58  bottom=  4.98
```

URP 17 已内置 `FullScreenPassRendererFeature`（可用，别自己写 RendererFeature）：

```
Library/PackageCache/com.unity.render-pipelines.universal@ee0b6bbace10/Runtime/RendererFeatures/FullScreenPassRendererFeature.cs
```

字段：`injectionPoint`、`requirements`、`passMaterial`、`passIndex`、`fetchColorBuffer`、`bindDepthStencilAttachment`。
`MistbellRenderer.asset` 现在的 `m_RendererFeatures` 是空数组。

## 要做什么

### 1. 新着色器 `spike/project/Assets/Art/Shaders/TiltShift.shader`

- Shader 名 `Mistbell/TiltShift`；**全屏 blit 着色器**，照 URP 官方 Blit 约定写：

```hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
```

  顶点用 `Blit.hlsl` 的 `Vert`（`#pragma vertex Vert`），片元用 `SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv)`，
  UV 用 `_BlitScaleBias` 换算（`Blit.hlsl` 的 `Varyings` 里没有时自行 `uv * _BlitScaleBias.xy + _BlitScaleBias.zw`）。
  单 Pass，`ZTest Always`、`Cull Off`、`ZWrite Off`。

- **模糊**：单 Pass 多抽样（不要求多次 RenderPass）。用 24 个黄金角螺旋采样点：
  `offset = float2(cos(a), sin(a)) * r * _ScreenParams.zw * radiusPixels`，
  `radiusPixels = _MaxRadius * band`，`_MaxRadius` 默认 **10.0**（1080p 下约 1% 屏高）。
  采样点数固定 24（循环里 `[unroll]`），中带 radius≈0 时直接返回原样（避免无谓采样）。

- **带形遮罩**（屏幕纵向，与相机无关）：

```
float d  = abs(uv.y - _FocusCenter);                       // _FocusCenter 默认 0.5
float m  = saturate(smoothstep(_FocusBand, _FocusBand + _Falloff, d));
float band = pow(m, _MaskPower);
```

  默认值（按现在这版构图算的，**别改**）：`_FocusCenter 0.5`、`_FocusBand 0.26`、`_Falloff 0.24`、`_MaskPower 1.2`、`_MaxRadius 10`。
  依据：教堂在画面里占 49.8% 高（uv.y 0.27–0.77），主体必须保持清晰，所以中带留 ±0.26，
  模糊往上/下边缘渐进——最底 5% 的草地得到 ~1.0 的模糊，教堂塔尖只有 ~0（不能把主体糊掉）。

- 只输出颜色；**不要**改亮度/对比/饱和（后期 Volume 已有一整套，重复调色会崩基线）。

### 2. 编辑器接线：`SpikeURP.TiltShift()`（加在 `spike/project/Assets/Editor/SpikeURP.cs`）

新增 `public static void TiltShift()`，batchmode 跑，做四件事（幂等，可重复执行）：

1. 材质资产 `Assets/Settings/M_TiltShift.mat`（shader = `Mistbell/TiltShift`，属性用上面的默认值）不存在就建；
2. **RendererFeature 存成独立资产** `Assets/Settings/TiltShiftFeature.asset`（`FullScreenPassRendererFeature` 实例，
   `injectionPoint = AfterRenderingPostProcessing`、`requirements = None`、`fetchColorBuffer = true`、
   `passMaterial = M_TiltShift.mat`、`passIndex = 0`）。
   ⚠️ 必须用独立 `.asset` 文件，**不要**做成 renderer data 的子资产——本项目已经踩过
   「VolumeProfile 子资产不进构建包，运行时时效果全丢」的坑（见 `spike/PROGRESS.md` 22:46 那条）。
3. 把 feature 加入 `Assets/Settings/MistbellRenderer.asset` 的 `rendererFeatures`（已存在则不重复添加），
   `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets` + `AssetDatabase.ImportAsset`。
4. 重新加载渲染器数据做断言并打日志：

```
[POST] tiltshift feature=Assets/Settings/TiltShiftFeature.asset material=M_TiltShift injection=AfterRenderingPostProcessing
[POST] renderer features=1 entry0=FullScreenPassRendererFeature materialSet=True
```

   若 `rendererFeatures.Count != 1` 或 `passMaterial == null` 就抛异常（不要静默通过）。

### 3. 运行顺序（先接线，再重建场景/出包，否则播放器里没有这个 pass）

## 验收标准（每条自己跑，贴数字）

```bash
cd ~/workspace/mistbell
U=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity

# 1) 接线（一次即可，幂等）
$U -batchmode -quit -projectPath spike/project -executeMethod SpikeURP.TiltShift -logFile spike/logs/97_post.log
grep -E "\[POST\]" spike/logs/97_post.log

# 2) 重建场景 + 出包
$U -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/98_scene.log
$U -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/99_build.log
grep -E "Build (Succeeded|Failed)|error CS" spike/logs/99_build.log

# 3) 运行时截图（必须 caffeinate）
caffeinate -dimu -t 150 bash spike/tools/capture_runtime.sh

# 4) 指标
python3 spike/tools/sharp.py spike/project/Shots/art_runtime.png spike/project/Shots/art_close.png spike/project/Shots/art_ground.png
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png spike/project/Shots/art_close.png spike/project/Shots/art_ground.png
python3 spike/tools/edge.py --skip=600:1300 spike/project/Shots/art_runtime.png
```

| 指标 | 门槛 |
| --- | --- |
| `sharp.py` 每张图的 **middle** hf | 与基线相差 **≤ 12%**（4.58 / 15.76 / 12.58） |
| `sharp.py` **top/mid** 与 **bottom/mid** | 相对基线**下降 ≥ 30%**（runtime 侧重看 bottom/mid，从 1.69 降） |
| `frame.py` `art_runtime` `sky` | 仍是 (167,183,203) ± 3（模糊不该改天空基色） |
| `frame.py` `art_runtime` `ground_near` | (68,90,28) ± 8 每通道（模糊会让方差降，均值允许小漂） |
| `frame.py` 三张图 `clipped>=250` | < 1.0% |
| `edge.py --skip=600:1300` mean jump | 仍 < 12（地平线软过渡不能被破坏） |
| 双平台构建 | macOS + Windows 均 `Succeeded`，`error CS` = 0 |
| `[SPIKE] frame` church `heightPct` | 49.8 ± 1.0（相机没动） |

## 边界

- 不要改 `SpikeArt.cs`、地形网格、教堂几何、雾参数、相机、光照、Volume 数值。
- 不要改 `tools/` 下的验收脚本（`sharp.py` / `frame.py` / `edge.py` 是**验收方**的尺子）。
- 改不到就如实报「未达标 + 原因」，不要改门槛。

## 报告要求

列出：改了/新增了哪些文件、上表每条的实测值（贴命令输出原文）、有没有没达标的；若模糊看起来有效但数值没到，
说明是「带形遮罩位置不对」还是「采样半径太小」，给出你建议的下一个参数值。
