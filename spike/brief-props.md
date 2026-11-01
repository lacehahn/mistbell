# 简报：前景细节物件（队列 15，Codex 第 21 轮）

## 背景（只读这一段就够，不要去通读仓库）

Mistbell 的 HD-2D spike 已经有：Blender 教堂、程序化像素材质（草/石/瓦）、SSAO、带状移轴、地形距离雾。
**现在主视角最大的短板是前景空无一物**：草地是一整片平涂（实测前景矩形 `Lstd=3.6 / 4.4`，几乎没有结构），
移轴模糊没有东西可虚，HD-2D 的「微缩模型」观感损失一半。这一轮只做**前景/路边物件**。

## 必读文件（只读这三个，不要通读仓库）

1. `spike/project/Assets/Editor/SpikeScene.cs` —— 只关心 `Run()`（材质/相机/机位表）、`BuildStonePath()`、`GroundY()/EyeAt()`、`ImportChurch()` 不动
2. `spike/project/Assets/Editor/SpikeArt.cs` —— `MeterUVBox` / `Box(parent,name,size,localPos,mat,collider,yaw)` / `MakeTextured(path,colorMap,normalMap,metersPerTile,fallbackColor,metallic,smoothness)` / `ImportTex`
3. `spike/tools/make_textures.py` —— `make_grass()` 是最接近的模板；`main()` 的 jobs 列表；`seam_score()` / `write_png()` / `normal_map()`

## 关键几何（已算好，直接照用，不要自己另推）

- 揭示机位（主视角 `art_runtime`）：`eye = EyeAt(28f, -100f)` ≈ `(28, -8.61, -100)`，看向 `(0, 13, 0)`，FOV 35（垂直），1920×1080。
  据此：`forward = (aim - eye).normalized`，`right = new Vector3(forward.z, 0f, -forward.x).normalized`（Unity 左手系，forward=(0,0,1) 时 right=(1,0,0)）。
- 屏幕纵向像素/米：`px_per_m(d) = 1712.6 / d`（d 为沿视轴距离，米）。
  → 12 m 处 143 px/m、20 m 处 86 px/m、40 m 处 43 px/m、110 m（教堂）处 ~16 px/m。
- **画面最下 15% 行（y=918..1080）对应地面在相机前方 9.2–16.3 m**。这一带就是「前景带」，物件必须落在这里才吃得到移轴虚化。
- 石板路 `BuildStonePath()`：13 个点，`z` 从 -70 → -13.25，`x = 3·sin(2πt)`，路面宽 3.2 m。路两侧 `|offset| ≥ 2.4 m` 是空的草地。
- 教堂正面在 `z≈-24`（台地），主视角中占屏高 `heightPct ≈ 49.8%`——**所有物件不得进入教堂剪影范围**（见验收 G5）。

## 任务

### A. 新贴图 `bush`（`spike/tools/make_textures.py`）

在 `main()` 的 `jobs` 里加 `("bush", make_bush, 53)`，新写 `make_bush(rnd)`（照 `make_grass` 的写法）：
- 128×128，可平铺；深绿三色阶 + 叶簇：约 260 团簇，每团 3–7 px 的不规则块，亮部/暗部交替；少量 `(0x8A,0x96,0x5A)` 干叶与 1 px 高光；逐像素颗粒 `±10`（三通道共用同一偏移，避免彩线）
- 目标均值落在 `R < G`、`G` 最高的深绿（例如 RGB ≈ 42/58/32 附近），**不要霓虹绿**
- 返回 `(img, 1.2)`，法线自动由 `normal_map` 生成
- 门槛：`seam_score` 两轴 ≤ 1.6（自己跑一遍并打印）

### B. 场景里的物件（`SpikeScene.cs`，新增 `static void BuildProps(MeshCollider terrain, Material stone, Material wood)`）

在 `BuildStonePath(...)` 之后调用一次；`bush` 材质用 `SpikeArt.MakeTextured(... TEX+"bush.png", TEX+"bush_n.png", 1.0f, Hex("#3A4A24"), 0f, 0.7f)`，
**`_BaseColor` 必须在 `SpikeScene.cs` 里显式 `SetColor` 写死**（`.mat` 被 `.gitignore` 忽略，写进 `.mat` 等于不可复现）——这点已在屋顶上踩过坑。
所有落点必须**射线吸附到地形**（`terrain.Raycast(new Ray(p + Vector3.up*100f, Vector3.down), out hit, 300f)`），不许悬空或埋进坡里。

**B1 前景带（主视角最下 15%，最关键）** —— 相对揭示机位摆放：
`p = eye + forward*f + right*l`，`f ∈ [9, 17]`、`l ∈ [-18, -3.2] ∪ [3.2, 18]`（**禁止 |l| < 3.2，保持视轴走廊干净**）。
- 灌木 12 丛：每丛 3 个盒体错位叠放（底 0.9–1.4 m 宽 × 0.35 m 高，中 0.7 m × 0.32，顶 0.45 m × 0.28），各自随机 yaw，整体底边埋入地面 0.08 m；总高 0.7–1.0 m（此距屏幕高 100–170 px）
- 石块 16 块：0.3–0.7 m 的盒体，随机 yaw + 轻微 roll，约 40% 埋入地面；复用 `stone` 材质（不要再造材质）
- 顺序用固定种子 `new System.Random(20261102)`，**同一份代码两次生成必须完全一致**（不许用 `Random.Range`/`Time`）

**B2 路侧（让 `art_close` / `art_ground` 也有前景）** —— 沿石板路点偏移：
从 `t=0` 到 `t=0.85`，每约 6 m 一处，左右交替，`|offset| ∈ [2.4, 4.2] m`：
- 灌木 10 丛（同上构造，尺寸可 0.8–1.2 倍）
- 石块 14 块
- **木栅栏**：沿路的 `-x` 侧（`offset = -2.6 m`）从 `z=-66` 到 `z=-16`，每 1.6 m 一根立柱
  （0.12 × 0.95 × 0.12 m 盒体，`wood` 材质），相邻柱之间两根横杆（截面 0.08 × 0.06 m，长度 = 柱距，高度 0.42 m 与 0.74 m，
  用 `Quaternion.LookRotation(delta)` 对齐路面方向），每一段各自射线吸附端点高度，倾斜跟随地形
- `wood` 材质补一句 `SetColor("_BaseColor", ...)` 到灰褐（ weathering，避免突然出现一排纯黑柱）

**B3 落地日志（验收要 grep）**：
```
[SPIKE] props bushes=<N> rocks=<N> fencePosts=<N> fenceRails=<N> seed=20261102
[SPIKE] propScreen nearMinY=<主视角中前景件最小屏幕 y> 
```
并打印主视角里「前景带内物件的屏幕包围盒并集」（x0,y0,x1,y1，像素），用于确认它们确实落在 y ≥ 918 的行里。

### C. 不要动的东西

相机（`Fov`、机位表、`EyeAt`）、光照（sun/ambient）、后期 Volume、`TiltShift` / `SSAO` / `TerrainHaze` 着色器、
教堂导入与材质、`grass/stone/roof` 三张贴图的生成逻辑、远山与雾。只加不删。

## 你必须自己跑一遍的验收（全绿再报告）

```
cd /Users/lance/workspace/mistbell
python3 spike/tools/make_textures.py                     # 打印 bush 的 seam 两轴与字节数
python3 spike/tools/texstat.py spike/project/Assets/Art/Textures/bush.png
python3 spike/tools/seam.py spike/project/Assets/Art/Textures/bush.png
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/160_scene.log
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/162_build.log
caffeinate -dimu -t 200 bash spike/tools/capture_runtime.sh
python3 spike/tools/sharp.py spike/project/Shots/art_runtime.png spike/project/Shots/art_close.png spike/project/Shots/art_ground.png
python3 spike/tools/region.py spike/project/Shots/art_runtime.png 200,940,700,1070 1300,940,1800,1070
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png
```

门槛（**基线是我在 05:14 量的，你要超过/守住**）：

| # | 项 | 基线 | 要求 |
| --- | --- | --- | --- |
| G1 | `seam.py bush.png` 两轴 | — | ≤ 1.6 |
| G2 | `texstat.py bush.png` | — | `G` 通道最高、`sat` 0.20–0.55、`edge%` ≥ 5 |
| G3 | `[SPIKE] props` 计数 | 0 | bushes ≥ 20、rocks ≥ 26、fencePosts ≥ 30、fenceRails ≥ 30 |
| G4 | `SpikeScene.Run` 日志 | — | `church heightPct=49.8`（±1.5）、`pitch=11.8`（±0.3）、`pilgrim y==terrain y` 保持、`stone tiling=(0.2083,0.2083)` 未变 |
| G5 | 教堂没被挡 | — | `heightPct` 仍在 48.3–51.3；且目视主视角教堂立面完整 |
| G6 | `sharp.py art_runtime` bottom | **1.95** | ≥ 2.6（前景有结构了，模糊也有东西可糊） |
| G7 | `sharp.py art_close` middle | **12.85** | 12.0–13.5（主体不能被糊或变钝） |
| G8 | `region.py art_runtime` 两个前景矩形 `Lstd` | **3.6 / 4.4** | ≥ 8.0 |
| G9 | `frame.py art_runtime` | `sky(167,183,203)` `clipped 0.00%` | sky ±2、`clipped` ≤ 0.5% |
| G10 | 构建 | — | 双平台 `Succeeded errors=0`、日志中 `error CS` 计 0 |

## 报告格式

最后输出一张表：每条门槛 → 实测值 → 过/不过；外加一行「主视角里前景读到几个明确物件、像不像 HD-2D 的前景层次」。
**任何一条不过就如实写不过，不要改门槛数字，也不要为了让数字好看而调整无关参数。**
