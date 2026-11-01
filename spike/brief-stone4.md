# Codex 任务：石材 v4 —— 块间明度差太大（贴图本身是 2× 亮度跨度）

> 状态：**待派发**。v3 已把块放大到可数（主视角一块约 21 px），但立面读起来仍像「白块+深灰块的被面」。

## 已确认的（不要回退）

- 块尺度：`M_Stone.mat` `m_Scale` 已改 1/4.8（块约 1.05×1.2 m，主视角 21 px），这是对的，保留。
- v2 的砌法（4 皮 / 每条灰缝独立 1–5 px / 流痕 / 积尘 / 法线 256）保留。

## 这次的病根（贴图内部量的，与光照无关）

`patchwork.py` 直接量贴图 `stone_ashlar.png`（tile=48 ≈ 一块石面）：

```text
blockSpread=74.3%   neighborDiff=14.3%   fineGrain=24.36
```

`blockSpread` 是「块均值的 p95−p5 / 均值」——**74% 意味着块与块之间有接近 2 倍的亮度跨度**（均值 119，p5≈75、p95≈163）。
目标参考（`art-ref/target-front-hd2d.jpg` 墙面）同类指标约 50–60%，且其中大半来自墙面的受光梯度而不是块色；
真实粗石砌的块间明度差通常在 ±10% 以内。**渲染里那些「白块」就是这里来的**，不是光照、不是法线。

## 要做什么

### 1. `make_textures.py` 的 `make_stone`：收窄块间色差

- 每块基色明度抖动：`rnd.uniform(0.08, 0.10)` → **`rnd.uniform(0.025, 0.035)`**；
- 色相抖动：`±6°` → **`±2.5°`**；
- 块内饱和度上限：`min(0.18, base_s)` → **`min(0.12, base_s)`**；
- 块间灰缝的随机压暗 `-(25…45)` **保留**（灰缝要靠暗，块面不要靠暗/亮）；
- 其它（块宽分布、错缝、流痕、积尘、细颗粒、法线）**一律不动**——v2/v3 的颗粒与砌法是对的。

### 2. 把石材 tiling 从 `.mat` 挪进代码（可复现性修复）

`spike/project/Assets/Settings/M_Stone.mat` 被 `.gitignore` 忽略（`spike/project/Assets/*`），
所以现在 tiling 只存在于工作区的一份未跟踪资产里，**新克隆的仓库跑 `SpikeScene.Run` 会回到 2.4 m**。

在 `spike/project/Assets/Editor/SpikeScene.cs` 里 `var stone = ChurchMaterial("stone", "#C9A87C");` 之后加两行（tiling = 1/4.8）：

```csharp
stone.SetTextureScale("_BaseMap", new Vector2(1f / 4.8f, 1f / 4.8f));   // 一张贴图覆盖 4.8 m（4 皮 × 1.2 m）
stone.SetTextureScale("_BumpMap", new Vector2(1f / 4.8f, 1f / 4.8f));
```

（若法线槽的属性名不是 `_BumpMap`，用材质里实际的名字；顺便打印一行 `[SPIKE] stone tiling=` 的日志便于复算。）

### 3. 只在必要时调移轴半径

放大块之后前景台地凸出更多，`sharp.py` 的 `bottom/mid` 从 0.34 涨到 0.49（门槛 ≤0.45）。
**只允许**把 `TiltShift.shader` 的 `_MaxRadius` 从 `10` 提到 **`12`**（最多 12）来恢复背景虚化强度；
不要改 `_FocusBand / _Falloff`，不要改 Volume、相机、雾。

## 验收标准（每条自己跑，贴原文数字）

```bash
cd ~/workspace/mistbell
python3 spike/tools/make_textures.py
python3 spike/tools/texstat.py spike/project/Assets/Art/Textures/stone_ashlar.png
python3 spike/tools/seam.py spike/project/Assets/Art/Textures/stone_ashlar.png
python3 spike/tools/patchwork.py spike/project/Assets/Art/Textures/stone_ashlar.png 0,0,240,240 --tile 48
U=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
$U -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/D1_scene.log
$U -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/D2_build.log
caffeinate -dimu -t 150 bash spike/tools/capture_runtime.sh
python3 spike/tools/region.py spike/project/Shots/art_close.png 1400,200,1750,420 300,150,560,400
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png spike/project/Shots/art_close.png spike/project/Shots/art_ground.png
python3 spike/tools/sharp.py spike/project/Shots/art_close.png
python3 spike/tools/patchwork.py spike/project/Shots/art_runtime.png 700,470,900,600 --tile 21
grep -E "heightPct|stone tiling" spike/logs/D1_scene.log
```

| 指标 | 门槛 | v3 实测 |
| --- | --- | --- |
| 贴图 `patchwork tile48` **blockSpread** | **≤ 25%**（主目标） | 74.3% |
| 贴图 `patchwork tile48` **neighborDiff** | 8 – 16%（别调回平涂） | 14.3% |
| `texstat` stone **saturation** | ≤ 0.15 | 0.118 |
| `texstat` stone **edge%** | 4.0 – 9.0 | 4.4 |
| `seam.py` stone ratio | 都 ≤ 2.0 | 0.75 / 1.23 |
| `region.py` 两矩形 **R-B** | +38 … +46 | +46.6 / +39.8 |
| `region.py` 两矩形 **sat** | 0.24 – 0.30 | 0.280 / 0.281 |
| `region.py` 两矩形 **Lstd** | ≥ 32 | 44.5 / 47.5 |
| `sharp.py` **top/mid** | ≤ 0.45 | 0.43 |
| `sharp.py` **bottom/mid** | ≤ 0.45 | 0.49 ✗（允许用 `_MaxRadius 12` 修） |
| `frame.py` 三张 **clipped>=250** | < 1.0% | 0.01 / 0.50 / **1.18** ✗ |
| `frame.py` `art_runtime` **sky** | (167,183,203) ± 3 | 不变 |
| `[SPIKE] frame` church **heightPct** | 49.8 ± 1.0 | 49.8 |
| 双平台构建 | 均 `Succeeded`，`error CS` = 0 | 通过 |

## 边界

- 只许改：`make_textures.py` 的 `make_stone` 里那三个数值、`SpikeScene.cs` 的 stone tiling 两行（+ 一行日志）、必要时 `TiltShift.shader` 的 `_MaxRadius`。
- 不要改 `tools/` 下任何验收脚本、不改几何/相机/雾/Volume、不改 grass/roof/wood/mist。
- `sharp.py` 的 `middle` **不再设门槛**（块变大后该基线自然会降，属正常），但请把实测值贴出来。

## 报告要求

贴每条实测值原文 + `git diff --stat`。未达标项写原因，不要调门槛。
