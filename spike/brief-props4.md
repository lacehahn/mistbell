# 简报：叶簇破轮廓 + 主体细分 + 法线高频化（队列 23，Codex 第 24 轮）

## 背景（只读这段，不要通读仓库）

第 23 轮（`brief-props3.md`）把灌木的贴图与反照率修好了一半：贴图 `edge% 9.4→18.4`、削顶 0→0.00%、
灌木本体比草地暗（luma 61.1 vs 77.1）。但**目视仍判「长苔的绿石头」**。轮末做了一次反照率归零差分探针，
推翻了前两轮的猜测，真因现在是**量出来的三件**（不要再重新猜）：

1. **主体网格太稀**：`Blob(subdiv 1)` 只有 42 顶点 / 80 面，`RecalculateNormals()` 出来是一条连续渐变的法线场，
   表面必然出现大块缓慢明暗 —— 贴图再细也压不住。
2. **法线图是低频的**：`make_bush()` 的高度场主要来自 `fbm([32,16,8])` 底明度（叶簇只在 0.18–0.54 上叠一点），
   换算到世界 = 20–30 cm 波长；主视角 143 px/m 下就是 30–40 px 宽的波，读成「流纹」。
3. **141 片叶簇全部埋在主体轮廓内**：水平偏移只有 `0.42–0.62 × bodyRadius`，而 bodyRadius ≈ 0.7 m、
   叶簇半径只有 0.10–0.17 m → 叶簇外缘永远够不到主体外轮廓，**只贡献表面疙瘩、不贡献轮廓断口**。

本轮只做这一件事：把这三条各自修掉。栅栏、石块、落点分布、相机、光照、后期、教堂、其它贴图**一律不要动**。

## 基线（2026-11-02 06:08 的构建，commit 0b2c5e6，全部由我复跑确认过）

```
foliage art_ground  bush (360,540,780,620) mean=(63,67,45) luma=64.4 sat=0.339 Lstd=31.5 clipped=0.03%
                    grass luma=77.1 → ratio 0.84
foliage art_runtime bush (1300,820,1750,980) luma=79.0 Lstd=37.2 clipped=0.00% → ratio 0.97
bushmask（反照率归零差分掩码，93815 px = 4.52% 画面）
        mean(59,64,42) luma=61.1 sat=0.337 p5=21.8 p50=51.1 p95=132.7 clipped=0.00%
texstat bush.png    128x128 meanRGB(58,73,43) sat 0.407 coarseStd 3.15 edge% 18.4 · seam 1.14/0.83
sharp art_ground    top 2.72 / middle 12.10 / bottom 2.73      ← middle 是唯一还没过的门槛（要 ≥13.5）
sharp art_runtime   top 0.00 / middle 4.14 / bottom 4.50
frame               sky(167,183,203) clipped 0.00% heightPct 49.8 pitch 11.8 porch centerX 924.6
```

## 必读文件（只读这三个，不要读别的）

1. `spike/project/Assets/Editor/SpikeScene.cs` —— 只关心 `BuildProps()` 里灌木材质段（605–640 行）与 `void Bush(...)`（665–756 行）
2. `spike/project/Assets/Editor/SpikeArt.cs` —— `Blob(Vector3 size, float displace, int subdiv, bool flat, int seed)`（56–144 行）
3. `spike/tools/make_textures.py` —— 只改 `make_bush()`（159–209 行）

## 任务

### A. 主体网格加密（治「大块缓慢明暗」）

`Bush()` 里 8 个主体网格：

```csharp
bushMeshes[i] = SpikeArt.Blob(bushSizes[i / 2], 0.22f, 1, false, 6100 + i);
// 改成
bushMeshes[i] = SpikeArt.Blob(bushSizes[i / 2], 0.34f, 2, false, 6100 + i);
```

`subdiv 2` = 162 顶点 / 320 面，`displace 0.34` 让轮廓本身也带起伏。`Blob` 用参数做缓存键，会自动生成新 `.asset`，
旧的 42 顶点网格即弃用（不要删旧文件，`Assets/Settings` 下的网格是缓存）。石块**不动**（仍 subdiv 1 / flat）。

### B. 叶簇外推（治「轮廓没有断口」）

仍然保留现有 2 个主体 blob、6 个缓存叶簇网格、独立 `System.Random(9200 + index)`、每丛 5–8 片。

**唯一要改的是叶簇中心与"露出量"**。参考半径不能再用一个全局 `bodyRadius`：主体的横截面半径随高度变化，
顶部细、腰部粗。请：

1. 从两个主体网格的**世界变换后顶点**建一张「高度 → 横截面半径」表：
   按 `y` 分 8 段（等分 `bodyMin..bodyTop`），每段取该段内顶点的最大 `|xz|`；段内无顶点时用相邻段线性插值。
   下文记作 `bodyRadiusAtY(y)`。
2. 叶簇中心水平半径改成 `0.62–0.92 × bodyRadiusAtY(该片中心 y)`（此前固定 0.42–0.62 × 全局半径），
   方位角仍均匀随机；高度仍 `y ∈ [0.30,0.95] × 丛高`，其中**至少 3 片**取 `y ≥ 0.70 × 丛高`（去吃顶边轮廓）。
3. **露出量判据**（必须算，不许靠感觉）：对每片叶簇，取变换后顶点的
   `maxRad` = 最大水平半径、`minRad` = 最小水平半径、`protrude = maxRad − bodyRadiusAtY(片中心 y)`；
   `exposure = protrude / max(maxRad − minRad, 1e-4)`（即该片径向直径中落在主体轮廓外的比例）。
   **每丛至少 3 片 `exposure ≥ 0.40`**，且这 3 片里任意两片的方位角相差 ≥ 55°（不许全挤在一侧）。
   达不到就把该片中心半径往上顶（`0.92` 上限内）或换网格尺寸重抽，**不许降低门槛**。
4. 包络硬约束保持不变：所有顶点仍须满足 `|xz| ≤ bodyRadius × 1.35`（近景最近处离机位 9.2 m，
   再宽会糊到镜头上）与高度 `0.7–1.2 m`。超了就把该片的 `fit` 缩小 —— **不许挪中心**（挪了就丢断口）。
5. 日志（验收要 grep，`bushIndex == 22` 时打印一行）：

```
[SPIKE] bushExposure clumps=22 exposedMin=<每丛最少露出片数> exposedMax=<最多> meanExposure=<全部叶簇 exposure 均值 3 位小数> bodyR=[<最小>,<最大>] byHeight=true
```

### C. 法线图高频化（治「流纹」）

`make_bush()` 重写高度场，并把贴图从 128² 提到 **256²**：

- **尺寸**：`make_bush()` 内部用 `size = 256` 自己算，**不要改全局 `SZ`** —— 草地仍必须 128²
  （证据：`grass.png` 的 md5 前后必须一致，见 F14）。照 `make_stone(rnd, size=512)` 的写法：
  内部自己的 `st_noise(cell)` + 自己的 `img/height` 表 + 调用 `write_png/normal_map/preview` 时传入 256 的图。
- **高度场改成叶簇高度**（这是关键）：每个叶簇是一个**凸起**（中心最高、边缘落到簇间谷），
  不再是 `0.10 + 0.035 × fbm` 的平滑底：
  - 底：`height = 0.10 + 0.03 × noise`（`fbm([32,16,8])`，只当极低频底，权重 ≤0.3）
  - 叶簇：数量从 520 提到 **900–1100**（256² 下每簇 10–18 px ≈ 4–7 cm），
    每簇在新高度场上写 `height = 0.35 + 0.45 × (1 − outer)`（`outer` 是归一化椭圆半径），
    即簇心比簇间高 0.45 左右；颜色仍按 5 档调色板写（**色相/明度档位不要大改**，`sat ≤ 0.42` 守）。
  - 枝条：36–44 条 1 px 折线（256² 下 1 px ≈ 3.9 mm，枝条要更长：每次 8–14 px 一段），颜色 `(46,38,28)`，
    `height` 写到 **0.02**（枝比叶低，法线上读成缝）。
  - 透光缝：60–80 个 1 px `(110,124,84)`，`height` 写 0.0（低，不与叶簇抢高光）。
- **法线强度参数**：`make_bush` 现在返回 `(img, 1.2, height)`。因为高度场已经改成阶梯状，
  把 strength 降到 **1.0**，靠 `bumpScale` 抬：
- **材质**：`MakeTextured(...)` 的最后一参 `bumpScale` **0.35 → 0.9**（`SpikeScene.cs` 609–610 行）。
  `smoothness` 保持 0。改完在 `[SPIKE] bushAlbedo` 那行日志末尾把 `bump=` 的值同步成 0.9。
- 目标统计（我复跑 `texstat`/`seam` 卡）：`sat ≤ 0.42`、`edge% ≥ 18`（不低于现在的 18.4 太多）、
  `coarseStd ≥ 3.0`、`meanRGB` 的 G ∈ [58,80]、`seam` 两项都落在 0.7–1.3。

## 不要动

栅栏（33 柱 / 64 横杆）、石块（30）、`NearPoint`/`RoadPoint` 的落点范围与数量、22 丛计数、
相机与 `ShotRig`、光照、Volume、SSAO/移轴参数、所有 shader、教堂 FBX、草地/石材/屋顶贴图
（这 6 个文件的 md5 必须前后一致）。`_BaseColor` 与反照率校正**不要重新调**（实测已达标：掩码内 luma 61.1，
比草地暗 0.79，clipped 0.00%）；如果 A/B/C 做完亮度比漂出 0.75–1.15，报告里如实写，不要用改反照率去凑。

## 你必须自己跑一遍的验收（全绿再报告，不许改门槛数字）

```bash
cd /Users/lance/workspace/mistbell
pkill -f "Unity.app/Contents/MacOS/Unity" ; sleep 2
md5 -q spike/project/Assets/Art/Textures/{grass,stone_ashlar,roof_slate,grass_n,stone_ashlar_n,roof_slate_n}.png > /tmp/md5_before.txt
python3 spike/tools/make_textures.py
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/180_scene.log ; echo SCENE=$?
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/182_build.log ; echo BUILDAPP=$?
caffeinate -dimu -t 240 bash spike/tools/capture_runtime.sh
python3 spike/tools/foliage.py
python3 spike/tools/texstat.py spike/project/Assets/Art/Textures/bush.png
python3 spike/tools/seam.py spike/project/Assets/Art/Textures/bush.png
python3 spike/tools/sharp.py spike/project/Shots/art_runtime.png spike/project/Shots/art_ground.png
python3 spike/tools/region.py spike/project/Shots/art_runtime.png 200,940,700,1070 1300,940,1800,1070
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png
grep -h "error CS" spike/logs/180_scene.log spike/logs/182_build.log | wc -l
md5 -q spike/project/Assets/Art/Textures/{grass,stone_ashlar,roof_slate,grass_n,stone_ashlar_n,roof_slate_n}.png | diff - /tmp/md5_before.txt && echo MD5_OK
grep -h "\[SPIKE\] props\|\[SPIKE\] bushLeaves\|\[SPIKE\] bushExposure\|\[SPIKE\] blobs\|heightPct\|pilgrim y=\|stone tiling" spike/logs/180_scene.log
# 再跑一遍 SpikeScene.Run（只跑场景，不用出包），上面这几行必须逐字相同（确定性）
```

| # | 项 | 基线 | 门槛 |
| --- | --- | --- | --- |
| F1 | `[SPIKE] bushLeaves` | total 141 / min 5 / max 8 | `total ≥ 110`、`min ≥ 5`、`max ≤ 8`、`meshes=6`、`height` 落在 0.7–1.2 |
| F2 | `[SPIKE] bushExposure` | 新增 | `exposedMin ≥ 3`、`meanExposure ≥ 0.30`、`byHeight=true` |
| F3 | `[SPIKE] blobs` | verts 42 / tris 80 | `verts=162`、`tris=320`（主体 subdiv 2 生效） |
| F4 | `foliage art_ground` 亮度比 | 0.84 | **0.75–1.15** |
| F5 | `foliage` clipped（两张） | 0.03% / 0.00% | 都 **≤ 0.05%** |
| F6 | `texstat bush.png` | 128² / sat 0.407 / edge% 18.4 / coarseStd 3.15 | **256²**、`sat ≤ 0.42`、`edge% ≥ 18`、`coarseStd ≥ 3.0`、G ∈ [58,80] |
| F7 | `seam.py bush` | 1.14 / 0.83 | 两项 0.7–1.3 |
| F8 | `sharp art_ground` middle | 12.10 | **≥ 13.5**（`top`/`bottom` 是移轴带外、本就该低，不设门槛） |
| F9 | `sharp art_runtime` | bottom 4.50 / middle 4.14 | `bottom ≥ 3.8`；`middle` 变化 ≤ 12% |
| F10 | `region art_runtime` 前景两矩形 | sat 0.524 / 0.538（06:05 实测） | `sat ≤ 0.60`、`Lstd ≥ 22` |
| F11 | `frame art_runtime` | sky(167,183,203)、clipped 0.00%、heightPct 49.8、pitch 11.8、porch 924.6 | sky ±2、clipped ≤0.1%、heightPct ±1.5、pitch ±0.3、porch ±5 |
| F12 | 构建 | 双平台 Succeeded | macOS 与 Windows 都 `Succeeded`、`error CS` 计 0 |
| F13 | 确定性 | — | 连跑两次 `SpikeScene.Run`，`props`/`bushLeaves`/`bushExposure`/`blobs`/`heightPct` 逐字相同 |
| F14 | 其它贴图 md5 | — | 草/石/瓦 6 个文件 md5 前后一致（`MD5_OK`） |
| F15 | 目视（**必须真读图**） | 「绿石头」 | 见下 |

F15 目视：用 `codex exec ... --image=spike/project/Shots/art_ground.png`（**必须用 `--image=` 等号形式**，
空格形式会把 prompt 当第二张图吞掉）把图读进来，回答四句：
① 灌木外轮廓能数出几处明显的叶团凹口（数出来，不要"有一些"）；
② 表面还有没有大面积光滑的明暗流纹/镜面反光带；
③ 顶边与两侧的轮廓是不是「一团团扇贝状」而不是一条连续曲线；
④ 一句话结论：现在读成「灌木丛」还是「绿石头」。

## 报告格式

1. 一句根因确认：`bushExposure` 的实测 `exposedMin` / `meanExposure` 是多少，与你抽到的方位角分布。
2. F1–F15 表格：每行 **实测值 / 过或不过**。不过就如实写不过，**不许改门槛、不许只报告过的那几条**。
3. 主体网格与叶簇的世界尺寸：主体 `bodyRadius` 实测区间、叶簇实际外径区间、每丛最高的 3 片 exposure。
4. 一句话：`art_ground` 里灌木现在读成了什么。
