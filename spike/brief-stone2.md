# Codex 任务：石材 v2（现在变成了「灰色抛光瓷砖」）

> 状态：**待派发**（v1 已验收：颜色与块宽合格，但丢了细颗粒、偏冷、灰缝仍是等宽网格）。

## v1 做对了什么（保留）

- 米黄压掉了：墙面 `sat 0.415 → 0.242`、`R-B +57.7 → +31.5`，不再是米色乐高。
- 块宽分布真随机了：`blockW mean 0.6000 std 0.2505`（std/mean 0.417 ≥ 0.28）。
- 脏污/流痕/底部积尘都在，`Lstd 39.4 / 38.6`（≥28 保留）。

## v1 新暴露的问题（都能量）

放大 `art_close`（crop 1180,120→1780,480）后看得很清楚：**每块石头是一块平滑色板，四边一圈统一深色描边 + 一圈浅色倒角高光** → 读成「灰色陶瓷砖 / 抛光石板」，不是 14 世纪粗石砌，也不是《歧路旅人》那种有颗粒的像素石。

| 问题 | 成因（`make_textures.py` 现状） | 度量 |
| --- | --- | --- |
| 表面太平滑，没有细颗粒 | 全图细噪声只有 `rnd.uniform(-4,4)`；`grain` 振幅 `±13`（`(grain-0.5)*26`） | `sharp.py art_close middle 15.76 → 11.05`（-30%） |
| 大块「迷彩」斑 | `blotch` 振幅 `(blotch-0.5)*144` = ±72，且 `cell = size//2` 只有 2×2 格 | 目视：整面墙有直径 0.5–1 m 的云状暗斑 |
| 每块统一描边 → 瓷砖感 | `bevel = -18 if edge==0 else (8 if edge==1 else 0)` 对所有块一视同仁 | 目视：每块都是一个闭合矩形轮廓 |
| 灰缝全暗且宽度几乎一样 | `joint = rnd.randint(2,4)` 但同一块四条边共用这个值；砂浆色恒为 `#4A4642` | 目视：等宽黑缝网格 |
| 上下环绕不连续 | `courses = 5`，256/5 = 51.2 px 不整除，最末行落在块体中间 | `seam.py` row ratio **3.11**（>2 判可见缝） |
| 法线被降采样糊掉 | 把 256 高度场降到 128 再喂 `normal_map`，砌缝倒角损失一半精度 | 目视：块边高光发软 |

## 要做什么

### 1. `make_stone`（`spike/tools/make_textures.py`）

- **皮数 5 → 4**：`courses = 4`（256/4 = 64 px/皮 = 0.6 m，整除，上下环绕必然落在灰缝里）。
  同时与文件头注释「stone_ashlar 1 张 = 2.4 m（4 皮石砌，每皮 0.6 m）」对齐。
- **拆掉统一描边**：
  - 灰缝像素按块随机压暗 `-(25…45)`；
  - 块体内边缘只留 1 px 随机轻微暗化 `0…-10`，**不要**再给每块固定 `+8` 高光；
  - 倒角只保留在高度场（法线）里，颜色上不再画轮廓。
- **灰缝宽度每条独立**：`1–5 px` 每条边单独抽；其中约 15% 的灰缝改用较亮砂浆色 `#6E665C`（填缝料，不是空缝）。
- **纹理振幅重排**：
  - `blotch` 振幅 `±72 → ±20`（消灭迷彩斑）；
  - `grain` 振幅 `±13 → ±22`，`cell = size//16`（16 px 级颗粒）；
  - 再加一层 `cell = size//32` 的细颗粒 `±10`；
  - 末端逐像素 `rnd.uniform(-4,4)` 提到 `±9`。
  目标：把 `sharp.py art_close middle` 从 11.05 拉回 **≥ 13.4**（达到 v0 的 15.76 附近更好）。
- **法线按 256 出**：给 `normal_map(img, height, strength, size=SZ)` 加一个 `size` 参数（默认 `SZ`，其它材质行为不变），
  stone 用 `size=size` 直接生成 256 法线，**删掉现在那段把高度场降采样到 128 的代码**。法线强度 0.9 保持。

### 2. 暖色（`SpikeScene.cs` 的 stone `_BaseColor` + `make_stone` 的基色）

- 设计文档对广场石材的目标色是**暖石 `#AFA084`**（`docs/01-art-direction.md` 的 Z06 调色板），即 `sat≈0.246 / R-B +43`。
  现在墙面 `R-B +31.5`，偏冷；但**不要回到 v0 的 0.415 / +57.7**（那是米黄乐高）。
- 手段：调 `base`（`colorsys.rgb_to_hls(0x8A/255, 0x84/255, 0x78/255)`）与 `_BaseColor`（现 `(0.62,0.60,0.57)`）二者，
  让渲染后的墙面落进上表门槛。**只准动 stone 的颜色**，roof / wood / grass 不动。

### 3. `roof_slate` 环绕（附带修，同一文件）

- `seam.py` 量得 `roof_slate` wrap ratio `col 9.39 / row 13.24`（wrap 行差 60.41、内部只有 4.56）——
  屋顶上每 2.4 m 就有一条可见的横向接缝线。把瓦排高度/横向排布改成整除 `SZ` 的整数，
  并保证首尾排、首尾列的图案连续。**不要重做瓦的配色与造型**，只修环绕。

## 验收标准（每条自己跑，贴原文数字）

```bash
cd ~/workspace/mistbell
python3 spike/tools/make_textures.py
python3 spike/tools/texstat.py spike/project/Assets/Art/Textures/stone_ashlar.png
python3 spike/tools/seam.py spike/project/Assets/Art/Textures/stone_ashlar.png spike/project/Assets/Art/Textures/roof_slate.png
U=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
$U -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/B1_scene.log
$U -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/B2_build.log
caffeinate -dimu -t 150 bash spike/tools/capture_runtime.sh
python3 spike/tools/region.py spike/project/Shots/art_close.png 1400,200,1750,420 300,150,560,400
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png spike/project/Shots/art_close.png spike/project/Shots/art_ground.png
python3 spike/tools/sharp.py spike/project/Shots/art_close.png
grep -E "heightPct" spike/logs/B1_scene.log
```

| 指标 | 门槛 | v1 实测 |
| --- | --- | --- |
| `region.py` 两矩形 **R-B**（暖度，主目标） | **+38 … +46** | +31.5 / +28.1 |
| `region.py` 两矩形 **sat** | 0.24 … 0.30 | 0.242 / 0.243 |
| `region.py` 两矩形 **Lstd** | ≥ 32（细节别糊平） | 39.4 / 38.6 |
| `sharp.py` `art_close` **middle** | **≥ 13.4**（v0 基线 15.76） | 11.05 ✗ |
| `sharp.py` top/mid 与 bottom/mid | ≤ 0.45（移轴仍要生效） | 0.43 / 0.43 |
| `seam.py` stone **ratio col / row** | **都 ≤ 2.0** | 1.60 / 3.11 ✗ |
| `seam.py` roof_slate **ratio col / row** | **都 ≤ 2.0** | 9.39 / 13.24 ✗ |
| `texstat.py` stone **coarseStd** | ≥ 24 | 24.55 |
| `texstat.py` stone **edge%** | 4.0 – 9.0 | 5.0 |
| `texstat.py` stone **saturation** | ≤ 0.18（配色交给材质） | 0.126 |
| `make_textures.py` 打印 blockW **std/mean** | ≥ 0.28 | 0.417 |
| `frame.py` `art_runtime` **sky** | (167,183,203) ± 3 | 不变 |
| `frame.py` 三张 **clipped>=250** | < 1.0% | 0.01 / 0.31 / 0.75 |
| `[SPIKE] frame` church **heightPct** | 49.8 ± 1.0 | 49.8 |
| 双平台构建 | 均 `Succeeded`，`error CS` = 0 | 通过 |

## 边界

- 只改 `make_textures.py` 的 `make_stone` / `make_roof` / `normal_map`（加 size 参数）与 `SpikeScene.cs` 里 stone 的 `_BaseColor` 一行。
- **不要**改 `tools/` 下的验收脚本（`texstat/region/frame/sharp/edge/seam` 是尺子）。
- **不要**动几何、相机、雾、移轴、Volume、grass / mist 的生成逻辑。
- 灰缝宽度、块宽分布、脏污流痕都保留 v1 的随机性，不要为了「好看」把块重新排成规则网格。

## 报告要求

列出：改了什么、上表每条实测值（贴原文）、未达标项与原因。**不要**为了通过而调整门槛或验收脚本。
