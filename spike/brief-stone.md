# Codex 任务：石材重做（它现在像「米色乐高砖」）

> 状态：**简报已写好、基线已量好，等待下一轮派发**（本轮只做了基线与工具，未派发，避免与下个调度撞车并行跑 Unity）。

## 问题（有数字）

`Shots/art_close.png` 近景把问题放大到无法回避：石墙是**等宽米色砖网格**，像贴纸/乐高，不像格鲁吉亚 14 世纪粗石砌。
两个独立成因，**都能量出来**：

1. **色调**：渲染出来的墙面 `mean=(138,111,80) sat=0.415 R-B=+57.7`（`tools/region.py` 取 x1400-1750,y200-420）。
   而贴图本身其实是中性灰 `meanRGB=(126,123,116) sat=0.090`（`tools/texstat.py`）——
   所以**偏黄不是贴图的错，是材质 `_BaseColor` 偏黄**：`SpikeScene.cs` 里
   `stone.SetColor("_BaseColor", new Color(0.6470588f, 0.5490196f, 0.4248366f, 1f))`。
2. **块太规整**：`coarseStd(32px)=17.61`（块级色调方差）偏低，块宽/块高分布太窄 → 视觉上是规则网格。

## 要做什么

### 1. `spike/tools/make_textures.py` 的 `make_stone(rnd)`

- 贴图从 **128×128 提到 256×256**（一张仍覆盖 2.4 m；`SpikeArt` 侧 tiling 不变，只换 PNG 分辨率与 .meta 重导）。
  `SZ` 是全局常量，请不要改它——给 `make_stone` 单独的尺寸参数，`value_noise` 的 `cell` 也按尺寸缩放。
- 砌法：**5 皮**（每皮 0.48 m），每皮块宽在 0.28–1.05 m 之间随机、标准差/均值 ≥ 0.28；每皮起点随机错开；
  灰缝宽度 2–4 px 随机（灰缝颜色深灰 #4A4642，不要纯黑）。
- 每块独立基色：在 `#8A8478` 附近游走（HSL 亮度 ±10%、色相 ±6°、饱和度 ≤0.18），块内再加一条自上而下的 3–6% 亮度渐变。
- 脏污：① 3–5 条竖向流痕（宽度 6–20 px、alpha 0.05–0.12 的深灰）；② 底部 20% 积尘渐变（整体压暗 8%）；
  ③ 全图细噪声 ±4/255。
- 保留现有的可平铺接缝处理与 `normal_map(img, height, strength)` 生成法线图的调用（法线强度可提到 0.9）。
- 生成时打印一行统计：`stone px=256 courses=5 blockW mean=.. std=.. min=.. max=..`。

### 2. 材质色（`spike/project/Assets/Editor/SpikeScene.cs`）

- 把 `stone.SetColor("_BaseColor", ...)` 那行的硬编码米色改成**中性灰褐** `new Color(0.62f, 0.60f, 0.57f, 1f)`。
- 屋顶（`roof`，红褐）与木（`wood`）**不要动**——屋顶的赭红是对的。

## 验收标准（每条自己跑，贴数字）

```bash
cd ~/workspace/mistbell
python3 spike/tools/make_textures.py            # 重生成贴图
python3 spike/tools/texstat.py spike/project/Assets/Art/Textures/stone_ashlar.png
U=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
$U -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/A1_scene.log
$U -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/A2_build.log
caffeinate -dimu -t 150 bash spike/tools/capture_runtime.sh
python3 spike/tools/region.py spike/project/Shots/art_close.png 1400,200,1750,420 300,150,560,400
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png spike/project/Shots/art_close.png
python3 spike/tools/sharp.py spike/project/Shots/art_close.png
```

| 指标 | 门槛 | 基线 |
| --- | --- | --- |
| `region.py` `art_close` 两个墙面矩形的 **sat** | **≤ 0.25** | 0.415 / 0.403 |
| 同矩形 **R-B** | **≤ +35** | +57.7 / +53.8 |
| 同矩形 **Lstd**（细节要有，别糊平） | **≥ 28** | 39.1 / 41.2 |
| `texstat.py` stone **coarseStd** | **≥ 24** | 17.61 |
| `texstat.py` stone **edge%** | 4.0–9.0（灰缝别消失也别变成噪点） | 6.4 |
| `texstat.py` stone **saturation** | ≤ 0.18（贴图本身仍偏中性，颜色交给材质） | 0.090 |
| `make_textures.py` 打印的 blockW **std/mean** | **≥ 0.28** | — |
| `frame.py` `art_runtime` `sky` | (167,183,203) ± 3 | 不变 |
| `frame.py` 三张图 `clipped>=250` | < 1.0% | 0.00 / 0.15 / 0.07 |
| `sharp.py` `art_close` middle | 15.76 ± 15%（移轴不能因贴图变化失效） | 15.76 |
| 双平台构建 | 均 `Succeeded`，`error CS` = 0 | — |
| `[SPIKE] frame` church `heightPct` | 49.8 ± 1.0 | 49.8 |

## 边界

- 只改 `make_textures.py` 的 `make_stone`（+ 物料色那一行），不要动 grass / roof / mist 的生成逻辑。
- 不要改 `tools/` 下的验收脚本（`texstat.py` / `region.py` / `frame.py` / `sharp.py` / `edge.py` 是尺子）。
- 不要动几何、相机、雾、移轴、Volume 数值。

## 报告要求

列出：改了什么、上表每条实测值（贴原文）、未达标的项与原因。**不要**为了通过而调整门槛。
