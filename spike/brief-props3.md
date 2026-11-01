# 简报：灌木的叶簇破轮廓 + 去塑感（队列 21，Codex 第 23 轮）

## 背景（只读这段，不要通读仓库）

第 22 轮把灌木/石块从「方盒塔」换成位移 blob 之后，方盒感消失，但**灌木现在读成「抛光的绿石头」**
（`art_ground` 里表面有一条条光滑的明暗流纹，像孔雀石）。本轮只做这一件事：**让灌木读成灌木**。
栅栏（33 柱 + 64 横杆）、石块、落点分布、相机、光照、后期、教堂、其它贴图**一律不要动**。

### 根因（已量好，不要重新猜）

1. **灌木的有效反照率几乎是黑的，所以画面里看到的亮度主要是高光**
   `164_scene.log` 里 `[SPIKE] tint bush ... tint=(4.06,3.03,4.19) guard=0.50 → albedo≈(0.11,0.14,0.07)`，
   但紧接一行 `bush.SetColor("_BaseColor", new Color(0.9f,0.95f,0.82f,1f))` 把 TintFor 的结果**整条覆盖**掉了：
   有效反照率 = `meanLin × 0.9/0.95/0.82` ≈ **(0.025, 0.046, 0.014)**，而草地是 (0.27, 0.35, 0.13) —— **暗 7 倍**。
   近黑反照率 + URP Lit 默认开着的 specular highlight → 看到的是一层宽而灰绿的镜面光，这就是「孔雀石」。
2. **`MakeTextured` 的参数位读错了**：第 6 个参数是 `smoothness`、第 7 个是 `bumpScale`。现在写的是 `0f, 0.7f`，
   即 **smoothness 已经是 0、bumpScale 是 0.7**。所以「smoothness 0.7 → 0.35」这条是错的，不要照做；
   光滑感的来源是上面第 1 条 + 法线图低频起伏（0.7 的 bumpScale 让 42 顶点的 blob 出现大块缓慢的明暗波）。
3. **贴图细颗粒太少**：`texstat bush.png` = `meanRGB(42,58,32) sat 0.445 coarseStd 2.03 edge% 9.4`，
   同帧草地 `edge% 17.7` —— 灌木表面的高频只有草地的一半，凑近看就是「矿石」。`make_bush()` 现在用 `fbm([16,8,4])` 打底，
   生成了大团缓慢渐变（这是「流纹」的第二个来源），叶簇只是 3–7 px 的小方块。
4. **轮廓没有破口**：主体是 1–2 个光滑 blob，外轮廓是一条连续曲线。

### 基线（2026-11-02 05:22 的构建，commit 62b8d04，用 `foliage.py` 量的）

```
art_ground   bush  (330,430,500,560)    mean=(122,125, 78) luma=121.3 sat=0.459 Lstd=43.2 clipped=0.99%
             grass (900,700,1300,900)   mean=( 64, 86, 25) luma= 77.1 sat=0.706 Lstd=14.7 clipped=0.00%
             ratio bush/grass luma = 1.57      ← 灌木高光带比草地亮 57%，所以「浮」
art_runtime  bush  (1300,820,1750,980)  mean=( 73, 96, 41) luma= 86.9 sat=0.574 Lstd=35.8 clipped=0.17%
             grass (700,860,1150,980)   mean=( 68, 90, 29) luma= 81.2 sat=0.677 Lstd= 5.1 clipped=0.00%
             ratio = 1.07
texstat bush  meanRGB(42,58,32) sat 0.445 coarseStd 2.03 edge% 9.4   · seam 0.79/0.84 OK
sharp  art_ground middle 12.21 · art_runtime bottom 3.84 · middle 4.58（移轴守卫）
```

## 必读文件（只读这三个，不要读别的）

1. `spike/project/Assets/Editor/SpikeScene.cs` —— 只关心 `BuildProps()` 开头到 `void Bush(...)` 结束（约 601–673 行）
2. `spike/project/Assets/Editor/SpikeArt.cs` —— `Blob()`（第 62–146 行）与 `MakeTextured()`（末尾）
3. `spike/tools/make_textures.py` —— 只改 `make_bush()`（第 159–183 行）

## 任务

### A. 叶簇几何（破轮廓）

在 `Bush()` 里保留现有 2 个主体 blob，**再加 5–8 片小叶簇**（子物体）：

- 网格：新增 6 个缓存网格，用现成的 `SpikeArt.Blob(size, 0.40f, 1, false, 9100 + i)`（`displace 0.40` 比主体的 0.22 大，
  这样每片自带叶团的凹凸）。三档尺寸 × 2 个形状变体（`size` 是**整体外径**，`Blob` 内部乘 0.5 得半径）：
  `(0.20,0.17,0.20)`、`(0.27,0.22,0.25)`、`(0.34,0.27,0.31)`。**不要**给每丛各建网格。
- 摆放（每片）：`yaw` 均匀随机；水平偏移半径 `0.42–0.62 × 主体水平半径`；高度 `y ∈ [0.30, 0.95] × 当前丛高`；
  其中**至少 2 片**要满足 `y ≥ 0.70 × 丛高`（专门去吃顶边轮廓）；姿态再加 ±20° 的随机俯仰/侧倾。
- 材质：沿用 `M_Bush` / `M_BushLight` 两档，按「片索引」奇偶交替（主体与叶簇各自交替，形成明暗咬合）。
- **随机数必须独立**：现在 `Bush()` 用 `shape = new System.Random(8100 + index)`，主体形状/位置/高度全靠它。
  叶簇请另建 `leaves = new System.Random(9200 + index)`，**不要**从 `shape` 里抽 —— 否则 22 丛主体的既有效果会整体漂移，
  `sharp`/`Lstd`/`seam` 的对照就失去意义。
- 总高仍要在 **0.7–1.1 m**（沿用现有 `heightScale` 钳制），叶簇不得把包围盒抬高超过 0.10 m；
  横向外扩不超过主体半径的 0.35 倍（近景灌木最近处离机位 9.2 m，`px_per_m = 1712.6/d`，再大会糊到镜头上）。
- 日志（验收要 grep）：
  ```
  [SPIKE] bushLeaves total=<总片数> min=<每丛最少> max=<每丛最多> meshes=6 height=[<最低>,<最高>]
  ```

### B. 材质（去塑感，且要**推导**不要试）

在 `BuildProps()` 的灌木材质段：

1. **关掉 specular highlight**（这是「抛光」的主因）：URP Lit 的开关是 `[ToggleOff] _SpecularHighlights`，
   `mat.SetFloat("_SpecularHighlights", 0f)` 之外**必须**同时 `mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF")`
   （该 keyword 是 `shader_feature_local_fragment`，光改 float 不生效）。`M_BushLight` 是拷贝出来的，记得同样处理。
2. **把反照率改回有意义的量级**。不要再手写 `SetColor("_BaseColor", (0.9,0.95,0.82))` 这种魔法数；
   两种做法任选其一，但必须留下推导注释：
   - 做法 1（推荐）：删掉那行覆盖，改用 `MakeTextured(...)` 的 `targetAlbedo` 参数直接给目标反照率。
     参考：草地实际反照率 `(0.27,0.35,0.13)`，**灌木应比草地暗**，建议起点 `(0.10,0.14,0.06)`（暗叶色）。
   - 做法 2：保留覆盖，但写成「按实测比例换算」的形式，注释里写清 `目标/实测`。
   然后**跑一次量一次**：用 `foliage.py` 量出 bush/grass 的 luma 比，若不在 0.75–1.10 内，按
   `_BaseColor *= 目标比/实测比` 调**一次**（最多一次），把两次的比值都写进报告。
3. `bumpScale` 0.7 → **0.35**（法线图的低频波正是一条条流纹的第二个来源）。

### C. 贴图（叶簇感）

重写 `make_bush()`（`make_textures.py`），要求：

- **5 档调色板**（深→亮，全部偏橄榄绿、别往翠绿跑）：
  `(18,28,17) (30,42,24) (44,58,34) (60,76,44) (84,100,62)`（最亮/最暗 luma 比 ≈ 3.5，要求 ≥ 2.2）。
- **叶簇尺度放大**：贴图 128 px 覆盖 1 m → 1 px ≈ 7.8 mm，主视角 143 px/m。
  用 **520 个月牙/椭圆叶簇，尺寸 6–11 px（≈5–8 cm）**，每簇压 1 档暗边（模拟自身的 AO），
  而不是现在的 3–7 px 方块（方块在小尺度像绒布）。
- **枝条**：15–22 条 1 px 折线（3–5 段，主体竖直偏斜），颜色 `(46,38,28)`，从下部往上穿、不铺满；
  这是「一眼认出是灌木」的关键形状线索。
- **透光缝**：30–40 个 1 px `(110,124,84)`（不要用纯天空色，否则像洞）。
- **降低频**：`fbm([16,8,4])` 只作为明度底，改成 `fbm([32,16,8])` 且权重 ≤ 0.5，不再主导画面。
- 法线图随之高频化（叶簇中心抬高、簇间压低），继续保持可平铺（`seam.py` 的 pair 必须留在 0.7–1.3）。
- 目标统计：`sat ≤ 0.42`（现在 0.445）、`edge% ≥ 16`（现在 9.4，草地 17.7）、`meanRGB` 的 G 落在 62–74。

### D. 不要动

栅栏、石块、`NearPoint`/`RoadPoint` 的落点范围与数量、22 丛/30 石的计数、相机与 `ShotRig`、光照、Volume、
所有 shader、教堂、草地/石材/屋顶贴图（这三个文件的 md5 必须前后一致）。

## 你必须自己跑一遍的验收（全绿再报告，不许改门槛数字）

```bash
cd /Users/lance/workspace/mistbell
pkill -f "Unity.app/Contents/MacOS/Unity" ; sleep 2
md5 -q spike/project/Assets/Art/Textures/{grass,stone_ashlar,roof_slate,grass_n,stone_ashlar_n,roof_slate_n}.png > /tmp/md5_before.txt
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/170_scene.log ; echo SCENE=$?
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/172_build.log ; echo BUILDAPP=$?
caffeinate -dimu -t 240 bash spike/tools/capture_runtime.sh
python3 spike/tools/foliage.py
python3 spike/tools/texstat.py spike/project/Assets/Art/Textures/bush.png
python3 spike/tools/seam.py spike/project/Assets/Art/Textures/bush.png
python3 spike/tools/sharp.py spike/project/Shots/art_runtime.png spike/project/Shots/art_ground.png
python3 spike/tools/region.py spike/project/Shots/art_runtime.png 200,940,700,1070 1300,940,1800,1070
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png
grep -h "error CS" spike/logs/170_scene.log spike/logs/172_build.log | wc -l
md5 -q spike/project/Assets/Art/Textures/{grass,stone_ashlar,roof_slate,grass_n,stone_ashlar_n,roof_slate_n}.png | diff - /tmp/md5_before.txt && echo MD5_OK
grep -h "\[SPIKE\] props\|\[SPIKE\] bushLeaves\|\[SPIKE\] bushAlbedo\|heightPct\|pilgrim y=\|stone tiling" spike/logs/170_scene.log            # 连跑两次 SpikeScene.Run，两行 log 必须逐字相同（确定性）
```

| # | 项 | 基线 | 门槛 |
| --- | --- | --- | --- |
| F1 | `[SPIKE] bushLeaves` | 无 | `total ≥ 110`、`min ≥ 5`、`max ≤ 8`、`meshes=6`、`height` 落在 0.7–1.2 |
| F2 | `foliage.py art_ground` bush/grass luma 比 | **1.57** | **0.75–1.10** |
| F3 | `foliage.py` bush `clipped` | 0.99%（ground）/ 0.17%（runtime） | **≤ 0.05%** 两张都要 |
| F4 | `foliage.py art_runtime` luma 比 | 1.07 | 0.75–1.10（不许因此变暗到看不见，下限 0.75） |
| F5 | `texstat bush.png` | sat 0.445 / edge% 9.4 | `sat ≤ 0.42`、**`edge% ≥ 16`**、G ∈ [62,74] |
| F6 | `seam.py bush` | 0.79 / 0.84 | 两项都落在 0.7–1.3 |
| F7 | `sharp art_ground` middle | 12.21 | **≥ 13.5**（叶簇 + 高频贴图应该把高频能量抬上去） |
| F8 | `sharp art_runtime` | bottom 3.84、middle 4.58 | `bottom ≥ 3.8`；`middle` 变化 ≤ 12%（移轴守卫，不许把主体糊了） |
| F9 | `region art_runtime` 前景两矩形 | sat 0.580 / 0.621、Lstd 24.3 / 25.9 | `sat ≤ 0.60`、`Lstd ≥ 22` |
| F10 | `frame art_runtime` | sky (167,183,203)、clipped 0.00%、heightPct 49.8、pitch 11.8、porch centerX 924.6 | sky ±2、clipped ≤ 0.1%、heightPct ±1.5、pitch ±0.3、porch ±5 |
| F11 | 构建 | 双平台 Succeeded | macOS 与 Windows 都 `Succeeded`、`error CS` 计 0 |
| F12 | 确定性 | — | 连跑两次 `SpikeScene.Run`，`props` / `bushLeaves` / `heightPct` 三行逐字相同 |
| F13 | 其它贴图 md5 | — | 草/石/瓦 6 个文件 md5 前后一致（`MD5_OK`） |
| F14 | 目视（**必须真读图**） | — | 见下 |

F14 目视：用 `codex exec ... --image=spike/project/Shots/art_ground.png`（**注意用 `--image=` 等号形式**）把图读进来，回答三句：
① 灌木外轮廓有没有明显的叶团凹口（数一下能看到几处）；② 表面还有没有大面积光滑的明暗流纹/镜面反光带；
③ 一句话结论：现在读成「灌木丛」还是「绿石头」。

## 报告格式

1. 一句根因确认：你改完后 specular 是否真的关掉了（证据：`clipped` 从 0.99% 变成多少）。
2. F1–F14 表格：每行 **实测值 / 过或不过**。不过就如实写不过，**不许改门槛、不许只报告过的那几条**。
3. `_BaseColor`（或 `targetAlbedo`）的**推导过程**：起点值 → 实测比值 → 最终值 → 最终比值。
4. 一句话：`art_ground` 里灌木现在读成了什么。
