# 派发：27a 第 3 轮 —— 雪山从「白纸山」变成「有雪有岩的山」，露岩从「黑雀斑」变成「石群」，补云带与地上长影

## 为什么重派（第 2 轮的实测结论，全部由我独立复跑得出）

第 2 轮（08:42 结束）自报 `[SCENE] magenta_px=0 warm_spots=179 snow_ratio=0.5053 sky_mean=0.5737` 四项全过，**但四个数字里三个是错的，一个尺子无效**：

| 指标 | 第 2 轮自报 | 我复跑（窗口+caffeinate 路径，同一构建） | 门槛 | 判定 |
|---|---|---|---|---|
| magenta_px | 0 | 0 | =0 | ✅ |
| snow_ratio | 0.5053 | **0.4771** | ≥0.50 | ❌ |
| sky_mean | 0.5737 | **0.5249** | 0.55–0.85 | ❌ |
| warm_spots | 179 | 面积≥40 的暖色块 **22** 个，其中真窗仅 **2** 个 | ≥6 | ❌ 尺子无效 |

根因是第 2 轮自己改了截图管线：给 `capture_runtime.sh` 加了 `-batchmode`、并新写 `BatchFrameReadback.cs` 走离屏 RT 回读。实测这条路径**整体比窗口路径亮 ≈10 luma，并把 23.38% 的画面推过 V>0.95 削顶**（窗口路径 0.47%），恰好把雪推过 `S<0.18 & V>0.72` 判定线。**此项已由我回退**：`capture_runtime.sh` 恢复窗口启动、`BatchFrameReadback.cs` 已移出工程。**本轮严禁再加 `-batchmode`、严禁再写离屏回读**——你的所有数字必须来自窗口路径。

`warm_spots` 口径也不成立：「R-B>35 的连通域个数」无面积下限，雪粒噪声、栅栏木料（2313 px 一大片）都算「一个暖光点」。本轮改用**面积 ≥120 px** 的对象计数。

## 目视判定（art_runtime.png，窗口路径）——四条硬伤

1. **山是白纸山**：三层全是同一个中性白（山体矩形 sat **0.035 / 0.046**、R-B −3.5 / −5.7），雪线 0.55 太低 → 65–220 m 的山体几乎全被雪色覆盖，岩石层完全消失；大三角面（n=65 × rows=9）折痕读成「折纸/低多边形」，三层之间也看不出前后（`_FogFloor` 0.10/0.35/0.68 在画面上读不出）。
2. **云带不可见**：`BuildMist()` 在 z=85/145、y=25/47，主视角里一条都看不见（第 1 轮就记过「雾带读不出来」，至今没修）。
3. **露岩 = 黑雀斑**：`BuildProps()` 的 150 颗 `ClusterRock_*` 用 `M_WinterRock #6E6255`，太阳仰角 20°，凸面除了顶上的雪帽之外全部背光 → 下半画面 `V<64` 的像素占 **15.0%**、面积 ≥60 px 的近黑板块 **99 个**，近景矩形 `p5 luma 21.8`。读成「白布上撒了一把黑棋子和黑芝麻」，不是石群。
4. **地上没有光的方向性**：`ground_shadow_gap` 0.036（门槛 >0.05），`shadows=180` 只在日志里成立，雪地上不见教堂/栅栏的长影；20° 暖光只落在窗上。

## 只许改这些文件

- `spike/project/Assets/Art/Shaders/RidgeFog.shader`
- `spike/project/Assets/Editor/SpikeScene.cs` —— 只限 `BuildMountains()` / `BuildMist()` / `BuildProps()` 里石头的材质与散布 / `BuildWarmDetails()`
- `spike/tools/make_textures.py`（如需新的岩石贴图）
- **不许动**：相机与 `ShotRig`、教堂 FBX/几何、角色、`Screenshotter.cs`、`capture_runtime.sh`、`docs/`

## 要做的事（每一条都对应上面的硬伤）

### A. 山：雪线抬高 + 岩石层 + 三层空气透视
- 雪线 `_SnowHeight` 0.55 → **0.72**，雪线下用岩石双向色（迎光 `#A8A29A`、背光 `#5B6470`，允许用贴图或法线表达块面），雪面迎光 `#EAF1F7`、背光 **`#B9C8D4`（必须有冷蓝偏移，不许中性白）**
- 网格加密：`n=65→120`、`rows=9→14`，脊线用 ≥3 个频段叠加，消除「大三角折纸」
- 三层按距离混雾色：layer1 混 25%、layer2 50%、layer3 78%（在 `RidgeFog.shader` 里加 `_Aerial`，颜色混向天空色 `#7B97B4`），使远层接近天空、前景层最实
- 天空色保持当前 `#7A96B4` 一带（当前实测 sky 均值 (113,139,163)）

### B. 云带：必须真的进画面
- 两条横向云带压在山脚与教堂之间，落在画面 **y 25%–45%** 高度带内，宽度铺满 ≥1200 px、边缘柔（alpha 由 `mist.png` × 垂直渐变），**不得抬亮天空**（天空矩形均值变化 ≤3）

### C. 露岩：从「黑棋子」改成「石群」
- 石头反照率 `#6E6255` → `#8E877D`（受光面要到 100–140 luma），雪帽面积增大（顶部法线 `y>0.45` 即盖雪）
- 散布改为**成簇**：每簇 3–8 颗，簇间距离 6–20 m 已有；本轮要求**小斑点消失**：面积 60–300 px 的近黑板块 ≤ 5 个
- 允许减少总数（150 → 90–120），宁可少而大

### D. 光与影
- 保持 sun 仰角 20°、色温暖、`shadows=180`，但必须**在雪地上看得见长影**：教堂左侧雪地竖带与右侧雪地竖带平均亮度差 ≥ 0.05（12.8/255）
- 4 处拱窗暖光自发光 + 2 个火盆，火盆要在雪地上留暖色光斑：门洞前地面矩形（取教堂正前方雪地 ≥ 200×100 px）`R-B ≥ 10`

## 硬门槛（全部用窗口路径截图自己量，逐条贴数字）

截图链（顺序不能变，**必须带 caffeinate，不许 -batchmode**）：
```
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/70_scene.log
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/71_build.log
caffeinate -dimu -t 260 bash spike/tools/capture_runtime.sh
```

| # | 指标 | 命令 | 门槛 | 当前实测（窗口路径） |
|---|---|---|---|---|
| 1 | magenta_px | `python3 spike/tools/measure_scene.py spike/project/Shots/art_runtime.png` | **=0** | 0 ✅ |
| 2 | snow_ratio | 同上 | **≥0.55** | 0.4771 ❌ |
| 3 | sky_mean | 同上 | **0.55–0.72** | 0.5249 ❌ |
| 4 | 全帧削顶 | `python3 spike/tools/measure_shot.py …` `white_clip_ratio` | **≤0.03** | 0.0047 ✅（不许靠提亮去过 2/3） |
| 5 | 暖光对象 | `python3 spike/tools/verify_scene.py …` | 面积≥40 的暖色块中，**bbox 高≥20 且宽≤20 的竖直窗形 ≥4** | 2 ❌ |
| 6 | 近黑板块 | 同上 `[DARK]` | **count ≤ 40**（现在 99） | 99 ❌ |
| 7 | 山体有彩 | `python3 spike/tools/region.py Shots/art_runtime.png 60,470,420,640 1450,400,1820,560` | **sat ≥0.10**、Lstd ≥25、p5/p50 ≤0.85、clipped ≤0.5% | sat 0.035 ✓Lstd 32.2 ✓p5/p50 0.661，**sat 不过** ❌ |
| 8 | 雪背光偏冷 | 取山体背光面矩形（自行给坐标并说明） | **R-B ≤ −8** | 未测，须补 |
| 9 | 地面长影 | `measure_shot.py` `ground_shadow_gap` | **≥0.05** | 0.036 ❌ |
| 10 | 云带 | `region.py` 云带矩形 | 均值介于天空与山雪之间（天空 luma +6 ~ +30）、`std ≤20` | 不可见 ❌ |
| 11 | 教堂构图未被动 | `grep "frame church" spike/logs/70_scene.log` | church heightPct 44–46、centerX 900–950、pitch 11.5–12.0 | 44.8 / 922.0 / 11.8 |

**任何一条不过就如实贴数字说明，不要为了让数字过而提亮整帧、不要改尺子、不要改口径。**

## 报告要求
贴出：① 上表 11 行的实测数字（每行带命令名）② `art_runtime.png`、`art_close.png` 路径 ③ 你改了哪些函数的哪几行 ④ 任何你发现的冲突或取舍。不 commit。
