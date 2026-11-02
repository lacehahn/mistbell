# 派发：雪面颗粒 + 天空亮度 + 右山岩色 + 窗形暖光（只做这四件，别动别的）

## 起点（2026-11-02 09:00 我在窗口路径独立复跑，md5 `97f682520d2f9c25e1e7f2ed399960d0`）

场景/构建/截图链均是干净的，数字如下（**这些是你的「改前」基线，不许把它们改坏**）：

| 指标 | 当前值 | 门槛 | 状态 |
|---|---|---|---|
| `measure_scene.py` snow_ratio | 0.6473 | ≥0.55 | ✅ 保持 |
| `measure_scene.py` sky_mean | **0.4499** | 0.55–0.70 | ❌ 要修 |
| `measure_shot.py` white_clip_ratio | 0.0003 | ≤0.03 | ✅ 保持 |
| `measure_shot.py` dark_ratio_lower | 0.0352 | ≤0.06 | ✅ 保持（黑雀斑已消，别再长回来） |
| `measure_shot.py` ground_shadow_gap | 0.0723 | ≥0.05 | ✅ 保持（长影已成立） |
| `region.py 300,900,900,1050`（近景雪地） | mean (198,200,196)、**Lstd 3.8**、p5 192.4 | Lstd ≥12 | ❌ 雪面是一张平白纸 |
| `region.py 60,470,420,640`（左山） | sat 0.208、Lstd 40.8、p5/p50 0.564 | sat≥0.10 | ✅ 保持 |
| `region.py 1450,400,1820,560`（右山） | **sat 0.054**、Lstd 19.1、p5/p50 0.594 | sat≥0.10、Lstd≥25 | ❌ 右山仍是中性白 |
| `verify_scene.py [WARM]` 窗形（bbox h≥20 且 w≤20） | **1 个**（488 px，box 932,598–943,641） | ≥4 | ❌ |
| 构图 | heightPct 44.8 / centerX 922.0 / pitch 11.8 | 44–46 / 900–950 / 11.5–12.0 | ✅ 保持 |

## 四件事

### 1. 雪面要有颗粒（最优先）
近景雪地 `Lstd 3.8` = 几乎均一色，读成「白纸」。要：给雪面加**0.15–0.4 m 低频风纹 + 细颗粒**（可以用一张 256² 可平铺 `snow.png` 走 `_BaseMap`，点采样、tiling ≈ 2.4 m；或在地形着色器里加两层错频噪声）。目标：近景雪地矩形 `Lstd ≥ 12`，同时 `dark_ratio_lower` 不得回涨（不要靠加深色斑点来凑方差）、`white_clip ≤0.05`。

### 2. 天空太暗
`sky_mean 0.4499`（上一版 0.5249、更早 0.5737），天空读成一块深蓝板。**只许改**：相机背景色 / `color.postExposure` / 天空相关的体积雾色，不许动机位与 FOV。目标 `sky_mean 0.55–0.70`，且天空 std 仍然平滑（不得出现云状噪声斑）。

### 3. 右山（第三层）要有岩色与明暗
右山 `sat 0.054 / Lstd 19.1` 是中性白团。原因大概率是第三层的 `_Aerial` 0.78 把颜色混得接近雾色，同时雪线 0.72 之上全是纯白雪色。要：第三层保留**可见的背光岩面**（冷蓝灰 `#6E7A88` 一带），并让雪脊与岩面之间**有硬边**（不是现在的柔和渐变）。目标：右山矩形 `sat ≥0.10`、`Lstd ≥25`、`p5/p50 ≤0.85`。

### 4. 窗形暖光至少 4 处
现在只有 1 个竖条形暖色块。4 个拱窗都要有暖光，且**形状要是竖窗**（bbox 高 ≥20 px、宽 ≤20 px），别让暗边框把发光嵌片大半遮掉。

## 硬约束
- **截图只能走窗口路径**：`caffeinate -dimu -t 260 bash spike/tools/capture_runtime.sh`。**严禁**加 `-batchmode`、严禁写离屏 RT 回读（实测那条路径整体亮 10 luma、削顶 23%，会让 snow/sky 两项虚假达标）。
- 开工前 `git status` 必须干净；改完**不要 commit**。
- 只许改：雪材质/`make_textures.py`（新增 `snow.png`）、地形着色器或 `M_Ground` 设置、`RidgeFog.shader` 的第三层配色、`BuildWarmDetails()` 的窗、相机背景色与 `postExposure`。
- **不许动**：`SpikeScene` 里 `BuildMountains()` 的网格尺寸（120×14 已定）、教堂 FBX/几何、角色、`ShotRig`/机位/FOV、`docs/`、`capture_runtime.sh`。
- 已加仓库级并发锁：`spike/tools/codex.sh` 会用 `spike/.codexlock` 串行化；如果看到 `ABORT: another Codex holds the repo lock`，说明另一会话正在改同一工作区 —— 直接退出，不要强写。

## 报告
逐条贴 6 个命令的实测输出（`measure_scene.py` / `measure_shot.py` / `verify_scene.py` / `region.py` 两个山体矩形 / `region.py` 近景雪地 / `grep "frame church"`），并给出 `art_runtime.png` 的新 md5。任何一条不过就如实写清数字。
