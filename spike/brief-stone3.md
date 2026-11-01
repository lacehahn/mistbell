# Codex 任务：石材 v3 —— 块尺度太小（不是颜色问题）

> 状态：**待派发**。v2 的颜色/方差已证明不是问题，问题是**石头本身太小**。

## v2 已验收通过的（不要回退）

`seam.py` stone `0.75/1.23`、roof `0.40/0.17` 全部 OK；`region.py` 墙面 `sat 0.293/0.286`、`R-B +39.9/+38.3`、`Lstd 46.6/45.6`；
`sharp.py art_close middle 13.54`（v0 基线 15.76）；`clipped 0.00/0.35/0.79%`；`church heightPct 49.8`；双平台 `Succeeded / error CS 0`。

## 真因（我量出来的，不是猜）

主视角 `art_runtime.png` 上教堂变成「迷彩拼布」，我先怀疑是 per-block 颜色差太大，于是建了 `spike/tools/patchwork.py` 量：

| 指标（块尺度对齐后） | 我们（v2） | 目标参考 `art-ref/target-front-hd2d.jpg` |
| --- | --- | --- |
| 邻块明度差中位数 `neighborDiff` | 5.4% | 10.8 – 14.2% |
| 块间饱和度离散 `blockSatStd/meanSat` | 26.2% | 26.2 – 28.2% |

**两项都与参考持平，甚至更保守** —— 所以颜色方差不是病根。

病根是**块的世界尺寸太小**：一张 stone 贴图覆盖 2.4 m、4 皮 → 每皮 0.6 m、块宽均值 0.53 m；
而教堂立面 34 m 宽，屏幕上是 660 px（`widthPct 34.4`），19.4 px/m → **一块石头在屏幕上只有约 12 px**，
整个立面 60+ 块 → 观感就是一整片细碎噪声（迷彩）。目标参考里石块约占建筑宽度的 9–10%（约 2.4–2.8 m 一块）。

## 要做什么（只有两个数字）

`spike/project/Assets/Settings/M_Stone.mat`：

- `_BaseMap` 的 `m_Scale` 由 `0.41666666`（= 1/2.4）改为 **`0.20833333`（= 1/4.8）**；
- 法线图槽（同文件第 55 行附近那条 `m_Scale: {x: 0.41666666…}`）同步改为 **`0.20833333`**。

效果：一皮 1.2 m、块宽均值约 1.05 m，主视角一块石头约 20–21 px（现在 12 px）—— 从「噪声」变成「可数的砌块」。

不要改贴图内容、不要改 `_BaseColor`、不要改动 `make_textures.py`（v2 的贴图是对的，只是铺得太密）。

## 验收标准（每条自己跑，贴原文数字）

```bash
cd ~/workspace/mistbell
U=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
$U -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/C1_scene.log
$U -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/C2_build.log
caffeinate -dimu -t 150 bash spike/tools/capture_runtime.sh
python3 spike/tools/region.py spike/project/Shots/art_close.png 1400,200,1750,420 300,150,560,400
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png spike/project/Shots/art_close.png spike/project/Shots/art_ground.png
python3 spike/tools/sharp.py spike/project/Shots/art_close.png
python3 spike/tools/patchwork.py spike/project/Shots/art_runtime.png 700,470,900,600 --tile 21
python3 spike/tools/patchwork.py spike/project/Shots/art_close.png 300,150,560,400 --tile 96
grep -E "heightPct" spike/logs/C1_scene.log
```

| 指标 | 门槛 | v2 实测 |
| --- | --- | --- |
| `patchwork.py` `art_runtime` 墙面 `neighborDiff` | ≤ 14%（不能再靠细碎块制造噪声） | 5.4% |
| `patchwork.py` `art_runtime` 墙面 `blockSatStd/meanSat` | ≤ 30% | 26.2% |
| `region.py` 两矩形 **R-B** | +38 … +46（不许回退成冷灰） | +39.9 / +38.3 |
| `region.py` 两矩形 **sat** | 0.24 – 0.30 | 0.293 / 0.286 |
| `region.py` 两矩形 **Lstd** | ≥ 32 | 46.6 / 45.6 |
| `sharp.py` `art_close` **middle** | ≥ 13.4 | 13.54 |
| `sharp.py` top/mid 与 bottom/mid | ≤ 0.45 | 0.40 / 0.34 |
| `frame.py` `art_runtime` **sky** | (167,183,203) ± 3 | 不变 |
| `frame.py` 三张 **clipped>=250** | < 1.0% | 0.00 / 0.35 / 0.79 |
| `[SPIKE] frame` church **heightPct** | 49.8 ± 1.0 | 49.8 |
| 双平台构建 | 均 `Succeeded`，`error CS` = 0 | 通过 |

## 边界

- **只改 `M_Stone.mat` 的两处 `m_Scale`**。不要动贴图、生成脚本、几何、相机、雾、移轴、Volume、grass / roof / wood / mist。
- 不要改 `tools/` 下的验收脚本。
- 如果改完 `Lstd` 掉到 32 以下（块变大后细节变少），**先如实报告，不要偷偷改门槛**，也不要回头去加噪声。

## 报告要求

贴出上表每条实测值原文、以及 `git diff --stat`（应该只有 1 个文件 2 行）。未达标项写原因。
