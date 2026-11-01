# brief-stone5 —— 石缝锐度 + 砂岩颗粒（队列 16）与屋顶色（队列 17）

## 你的任务边界（先读这三条，违反即视为无效交付）

1. **只允许改一个文件**：`spike/tools/make_textures.py`。
2. **禁止通读全仓**。必读文件只有：`spike/tools/make_textures.py`、`spike/tools/texstat.py`、`spike/tools/seam.py`。禁止 `ls -R`、禁止打开 `docs/`、禁止读 `SpikeScene.cs`/`SpikeURP.cs`。
3. **不许改**：随机种子（grass 11 / stone 23 / roof 37）、`good` 的 grass 分支、贴图的米制覆盖（grass 3.2 m、stone 4.8 m、roof 2.4 m 的 tiling 已经写在 C# 里）、任何 `.cs` 文件、任何 shader、相机、光照、SSAO、移轴。

## 背景：当前画面到底哪里不对（已用工具量化，不是主观感受）

主视角 `Shots/art_runtime.png`（1920×1080）里：

- **屋顶是番茄红 + 黑噪点**。屋顶区域 `920,430,965,462` 量得 `mean=(156,84,55) sat=0.638 R-B=+101.0 p5=56.4 p50=95.2 tail=0.593`。贴图本身 `texstat` 是 `meanRGB=(121,86,73) sat=0.396`。要压到「深石板青灰」。
- **石墙的近读是「现代砌块 / 石膏」**。`art_close.png`（143 px/m）里砖缝读成一条 8 px 宽**软灰带**，不是清晰凹槽 + 接触阴影；块面平滑，砂岩颗粒感弱。当前倒角是 **3 texel 阶梯**（`0.30 → 0.50 → 0.70`），在 256 tex / 4.8 m = 53.3 texel/m 下，1 texel = 2.68 屏幕像素，3 texel ≈ **8 px**——这就是那条灰带。
- 低频斑驳 `blotch` 振幅 88 把颗粒盖住了（`coarseStd` 主要来自块与块之间，`fineGrain` 不足）。

## 第 1 项：石缝锐度 + 砂岩颗粒（`make_stone`）

### 1.1 贴图尺寸 256 → **512**

- `512/4.8 m = 106.7 texel/m` → 在 `art_close`（143 px/m）下 **1 texel ≈ 1.34 px**，倒角 2 texel ≈ 2.7 px，才可能读成「清晰凹槽」。
- 4 皮 × 512 必须整除：`y1 = round((course+1)*size/courses)`，512/4 = 128 **恰好**，不许改成 3/5 皮。
- 自检断言：`stone_noise` 的三处 `size // 16 / size // 32 / size // 2` = 32 / 16 / 256，都能整除 512，不要写成固定数字。
- 块宽范围 `min_w = ceil(size*0.28/2.4)` = 60、`max_w = floor(size*1.05/2.4)` = 224（换算成米仍是 0.28–1.05 m，**块的真实宽度不许变**）。`std/mean >= 0.28` 的拒绝采样保留。
- `scale = 2.4/size` 的打印保留（它换算成米，值应仍是 mean≈0.53、min≈0.28、max≈1.00 附近）。

### 1.2 倒角 3 texel → **2 texel**，落差从 0.40 提到 0.80

现状：
```python
bevel = (0.30, 0.50, 0.70)[min(edge, 2)]
edge_dark = rnd.uniform(-12, -8) if edge == 0 else (-8 if edge == 1 else 0)
```
改成（数值是要求，写法自由）：
```python
bevel = (0.20, 0.80)[edge]          # edge==0 → 0.20，edge==1 → 0.80，edge>=2 → 1.0
edge_dark = rnd.uniform(-14, -10) if edge == 0 else 0   # 只留 1 列暗线，edge==1 不再压暗
```
理由：灰晕的成因是「3 档浅落差」把一条边摊成三列半亮的像素；改成「1 列很暗 + 1 列接近满高」，法线在缝边变陡，屏幕上才是凹槽而不是灰带。`edge==1` 再压 -8 是灰带的另一半来源，必须去掉。

### 1.3 灰缝宽度 1–5 px → **1–3 px**

```python
left_joint, right_joint, top_joint, bottom_joint = [rnd.randint(1, 5) for _ in range(4)]
```
→ `rnd.randint(1, 3)`。512 下 3 px = 2.8 cm 灰缝，仍然合理；5 px 在近读会宽到 6.7 px 屏幕。

**注意**：这一行改完会改变后续 `rnd` 序列（每个块消耗的随机数个数不变，因为仍是一次 `randint(1,3)×4`，序列长度一样），块宽/布局可能变——这是允许的，但必须重新跑一遍并确认块宽仍在 0.28–1.05 m、`std/mean>=0.28` 的断言没有触发死循环（有 `while True`，务必实测能退出）。

### 1.4 高频颗粒加权：`blotch` 88 → **50**，颗粒 44/20 → **34/40**

现状：
```python
variation = ((grain[y][x] - 0.5) * 44 + (fine_grain[y][x] - 0.5) * 20 +
             (blotch[y][x] - 0.5) * 88 - 7)
```
改成：
```python
variation = ((grain[y][x] - 0.5) * 34 + (fine_grain[y][x] - 0.5) * 40 +
             (blotch[y][x] - 0.5) * 50 - 7)
```
理由：`blotch` 是低频斑驳，振幅 88 在 512 下会摊成 4.8 m 尺度的大色块（比一块石头还大），把 1–2 px 的砂粒压没了。要让**高频**（`fine_grain`）占主导。

### 1.5 加的砂粒（可选但推荐，属本项验收的观感依据）

在现有 `if height[y][x] > 0.1 and rnd.random() < 0.008:` 的砂眼分支旁边，再加一类**亮砂粒**：`rnd.random() < 0.012` 时 `+rnd.uniform(10, 18)` 并 `height += 0.06`。不要沿块边界、不要成团。

### 1.6 顺手修 `seam_score` / `preview` 的尺寸 bug（**必须修，否则打印的是假数**）

现在这两个函数用全局 `SZ = 128` 索引，对 256 的石材已经在量「左上象限的内部边」，输出 `seam(col,row)=4.47,1.92` 是**无效数据**（`tools/seam.py` 按真实尺寸量才是 **0.64/1.30**）。改成按 `len(img)` / `len(img[0])` 取真实尺寸：

```python
def seam_score(img):
    sz = len(img)
    ...  # 所有 SZ 换成 sz
def preview(img, path, times=4, zoom=2):
    sz = len(img)
    ...
```
并在 `main()` 里打印时带上尺寸：`seam(col,row)@512x512=…`。要求：修完后 `make_textures.py` 对 stone 打印的两个 seam 比值与 `python3 tools/seam.py <png>` 的输出**一致（差 < 0.05）**。

## 第 2 项：屋顶色（`make_roof`）

目标：**深石板青灰**（Gergeti 是凝灰岩/玄武岩屋顶，不是陶瓦），并且**去掉黑噪点**。

### 2.1 色相/饱和度

当前：`(clamp8(v*210), clamp8(v*152), clamp8(v*130))`，`tone = 0.58 + rnd.uniform(-0.18, 0.18)`。

要求改成 HLS 生成，基准色 **hue 215°、lightness 0.33、saturation 0.10**，再乘颗粒。验收按贴图统计量（见下），不按代码写法。

### 2.2 明暗必须是**乘性**，不许再减绝对值

黑噪点的根因：`add(img, x, y0 + row_h - 1, -46)` 这类**减法**落在本来就暗的瓦上（`v` 可低到 0.40，RGB 只有 84/61/52），减 46 直接 clamp 到 10 以下 → 单像素黑点。

改法：
- 上缘受光唇 `×1.12`
- 下缘 3 px 压影 `×0.80 / ×0.88 / ×0.94`（越靠缝越暗）
- 竖向瓦缝 `×0.86`
- 瓦片明度抖动 `tone` 收窄到 `0.62 ± 0.08`（原来 ±0.18，暗瓦与亮瓦差 2.2 倍，主视角就是「花斑」）
- 保证任何像素不低于「该瓦基准色的 0.55 倍」

搭接感（上唇亮/下缘暗/竖缝）必须**保留**，这是 4 排瓦能被读出来的唯一线索。

### 2.3 尺寸

屋檐维持 128×128（`rows=4, row_h=32`，2.4 m/张 → 0.6 m 一排瓦）。**不许改尺寸**，改了就必然带动 `row_h`/`tw` 的整除关系，收益不抵风险。

## 验收标准（我会逐条独立复跑；任何一条不过就是未通过）

先跑生成器：
```bash
cd /Users/lance/workspace/mistbell/spike && python3 tools/make_textures.py
```
必须 exit 0、无异常、无死循环（1.3 s 是 256 的基线，512 应在 10 s 内）。

然后（每一条都要给数字）：

| # | 命令 | 门槛 |
| --- | --- | --- |
| 1 | `python3 tools/texstat.py project/Assets/Art/Textures/stone_ashlar.png` | `512x512`；`saturation ≤ 0.13`；`coarseStd(32px) ≥ 14`；`edge% ≥ 5.0` |
| 2 | `python3 tools/seam.py project/Assets/Art/Textures/stone_ashlar.png` | 两个 ratio 都落在 `[0.55, 1.80]`，且 `worst ratio` 那一行不报 FAIL |
| 3 | `python3 tools/texstat.py project/Assets/Art/Textures/roof_slate.png` | `saturation ≤ 0.16`；**蓝通道均值 ≥ 红通道均值 - 3**（`meanRGB` 里 B ≥ R-3）；亮度均值落在 `[55, 105]` |
| 4 | `python3 tools/seam.py project/Assets/Art/Textures/roof_slate.png` | 两个 ratio 都落在 `[0.50, 1.80]` |
| 5 | `python3 tools/make_textures.py` 自己打印的 stone seam 值 | 与第 2 条 `tools/seam.py` 的两个数**相差 < 0.05**（证明 1.6 的尺寸 bug 真修了） |
| 6 | `python3 tools/texstat.py project/Assets/Art/Textures/grass.png` | 必须仍是 `128x128 meanRGB=( 79, 90, 58) saturation=0.349`——grass 一个字节都不许变 |

## 不要做的事

- 不要为了过门槛去改 `tools/texstat.py`、`tools/seam.py`、`tools/region.py`（那三把尺子是我们的标定基准）。
- 不要动 C#：`SpikeScene.cs` 里的 `stone tiling=(0.2083,0.2083)` 已经对，贴图从 256 换 512 **不需要**改 tiling（tiling 是 UV/米，与贴图像素尺寸无关）。
- 不要跑 Unity、不要出包、不要截图——那一步我自己做。你的交付 = 改好的 `make_textures.py` + 重新生成的 6 张 PNG（在 `project/Assets/Art/Textures/` 下）。
- 不要 commit、不要 push。

## 交付时请在回复里列出

1. 你改动的最小 diff 摘要（哪几个函数、哪几行参数）。
2. 上表 6 条验收命令的**真实输出数字**（照抄终端，不要复述门槛）。
3. 若某条做不到，明说做不到 + 原因，不要假装通过。
