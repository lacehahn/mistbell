# 简报：灌木/石块换成不规则 blob 网格（队列 20，Codex 第 22 轮）

## 背景（只读这段，不要通读仓库）

上一轮（第 21 轮）已经加了前景物件（`SpikeScene.BuildProps()`），数量与构图都对，但**灌木是 3 个轴对齐方盒叠放**
（`art_ground` 里一眼看穿成「货架纸箱塔」），石块也是方盒随机转。这一轮只做一件事：**把灌木与石块换成位移后的低多边形 blob 网格**。
栅栏（33 柱 + 64 横杆）已经合格，**不要动它**。

## 必读文件（只读这两个）

1. `spike/project/Assets/Editor/SpikeScene.cs` —— 只关心 `BuildProps()`（本轮改这里面的 `Bush()` 与 `Rock()`）与 `GroundY()/EyeAt()`
2. `spike/project/Assets/Editor/SpikeArt.cs` —— `MeterUVBox` / `Box` / `MakeTextured`（照 `MeterUVBox` 的「UV 以米计」规则写 blob）

## 任务

### A. 新增 `SpikeArt.Blob(Vector3 size, float displace, int subdiv, bool flat, int seed)` → `Mesh`

1. **基础体**：二十面体（12 顶点 / 20 面），按 `subdiv` 次做中点细分并把新顶点归一化到单位球（`subdiv=1` → 42 顶点 / 80 面，这是要用的档位）。**必须焊接重合顶点**（用坐标量化 key），否则法线是碎的。
2. **位移**：对每个顶点方向 `d`，用**确定性**哈希（`Mathf.Sin` 组合或整数 hash，禁止 `UnityEngine.Random`）求 `h ∈ [0,1]`，
   半径 `r = 1 + displace * (h * 2 - 1)`；再叠一层更细的哈希（频率 ×3，幅度 ×0.4）打破球感。整体再乘非均匀缩放 `size * 0.5`。
3. **UV 以米计**：与 `MeterUVBox` 同一约定——按顶点**法线的主轴**取面内两个分量乘对应边长。材质 tiling 仍是 `1/米数`。
4. **法线**：`flat=false` 用平滑法线（灌木）；`flat=true` 把每个三角形顶点拆开再 `RecalculateNormals`（石块，要硬棱）。
5. **每个网格必须是真资产**（`AssetDatabase.CreateAsset` 或 `AddObjectToAsset`）或至少 `hideFlags` 后随场景保存，
   否则保存/重开场景后网格丢失（这条已经踩过：程序化网格必须落盘）。

### B. 改 `BuildProps()` 里的 `Bush()` 与 `Rock()`

- `Bush(parent, name, p, scale)`：改为 **2 个 blob 错位叠合**（下大上小：`size (1.0–1.5, 0.55–0.75, 1.0–1.5)` 与 `(0.6–0.9, 0.4–0.55, 0.6–0.9)`，
  水平错开 ±0.25 m、垂直错开 0.25–0.4 m），各自随机 yaw，`displace 0.22`、`subdiv 1`、`flat=false`，
  整体底边埋入地面 0.08 m；总高仍要落在 0.7–1.1 m（不然主视角尺度会变，`sharp`/`Lstd` 门槛会跟着漂）。
  网格按尺寸分档缓存复用（例如 4 档尺寸 × 2 个形状变体，共 8 个 Mesh），不要给 22 丛各建一个网格。
- `Rock(parent, name, p)`：`size (0.3–0.7)^3`、`displace 0.32`、`subdiv 1`、`flat=true`，随机 yaw/roll，约 40% 埋入地面（沿用现有埋深算法）。
- **灌木配色分两档**：新建 `M_Bush`（现有 `bush.png`）与 `M_BushLight`（同一贴图，`_BaseColor` 亮 ~1.25 倍、略偏黄绿），
  按索引交替使用——现在 22 丛全是一个色，远看是黑疙瘩。
- **不要动**：栅栏、落点分布（`NearPoint`/`RoadPoint` 的 f/l 范围）、数量、相机、光照、后期、shader、教堂、其它贴图。

### C. 日志（验收要 grep）

```
[SPIKE] props bushes=22 rocks=30 fencePosts=33 fenceRails=64 seed=20261102
[SPIKE] blobs meshes=<复用网格数> verts=<单网格顶点数> tris=<单网格三角数> bushVariants=8
```

## 你必须自己跑一遍的验收（全绿再报告）

```
cd /Users/lance/workspace/mistbell
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/164_scene.log
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/166_build.log
caffeinate -dimu -t 200 bash spike/tools/capture_runtime.sh
python3 spike/tools/sharp.py spike/project/Shots/art_runtime.png spike/project/Shots/art_ground.png
python3 spike/tools/region.py spike/project/Shots/art_runtime.png 200,940,700,1070 1300,940,1800,1070
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png
```

| # | 项 | 基线（第 21 轮） | 要求 |
| --- | --- | --- | --- |
| H1 | `[SPIKE] blobs` | 无 | `verts` 42–162、`tris` 80–320、`meshes` ≤ 12 |
| H2 | `sharp art_runtime` bottom | 3.43 | ≥ 3.4（不许倒退） |
| H3 | `region art_runtime` 两矩形 `Lstd` | 23.6 / 17.5 | ≥ 15.0 |
| H4 | `sharp art_ground` middle | 11.02 | ≥ 10.0 |
| H5 | `frame art_runtime` | sky (167,183,203)、clipped 0.00% | sky ±2、clipped ≤ 0.5% |
| H6 | `SpikeScene.Run` 日志 | `heightPct 49.8 / pitch 11.8 / pilgrim y=terrain y` | 全部保持（±1.5 / ±0.3 / 相等） |
| H7 | 构建 | 双平台 Succeeded | `errors=0`、`error CS` 计 0 |
| H8 | 确定性 | — | 连跑两次 `SpikeScene.Run`，`[SPIKE] props` 与 `[SPIKE] blobs` 两行完全相同 |

## 报告格式

门槛表（每行：实测值 / 过或不过）+ 一句话「`art_ground` 里灌木读成了什么」。**不过就如实写不过，不要改门槛数字。**
