# 派遣任务：教堂体量重建（真实尺寸）+ 贴地平视机位（D4 轮）

## 背景（必读，别通读全仓）

Mistbell 是 HD-2D 风格探索 RPG 的技术 spike，工程在 `spike/project`（Unity 6.3 / URP）。
当前场景 `SpikeScene.Run` 里教堂是**旧的 14 m 估算体量**：`Body 6.5×6.4×9`、`Tower 2.8×10×2.8`、`Spire` 在 y=13.4，
看起来像「带烟囱的小房子」，与参考图（格鲁吉亚 Gergeti 圣三一教堂）完全不像。

用户提供了风格定稿参考（**只看下面的数值摘要即可，不必打开图片**）：
- 真实尺寸：总宽 **34.0 m**、总高（含塔顶十字）**27.0 m**、进深 **24.0 m**
- 穹顶：**12 边形**鼓座 + 锥形顶，顶上十字
- 钟楼：**8 边形开敞**（顶层 8 个拱洞）圆锥顶，**独立于主体、位于右前方**
- 台地：教堂坐在挡土墙平台（台地）上，正面有台阶
- 相机：**接近平视，俯角 10–18°**（现在是 20–26°，太大）；FOV 35–40°

## 只允许改这些文件

- `spike/project/Assets/Editor/SpikeScene.cs`（主战场）
- 允许**新增** `spike/project/Assets/Editor/PrimitiveMeshes.cs`（多边形棱柱/锥体/山墙屋脊生成器）
- 允许小幅改 `spike/project/Assets/Scripts/ShotRig.cs`（仅在需要「位置 + 看向点」的机位时）
- **不要**改 `SpikeArt.cs` 的材质约定、`tools/make_textures.py`、`Screenshotter.cs`、任何 docs、任何 shader
- 不要新增贴图、不要新增材质文件（复用现有 `stone` / `roofMat` / `ground`）
- 不要动曝光/雾/后期参数（那是下一轮的事）

## 尺寸表（照抄实现，单位米，世界坐标）

教堂根节点 `Church` 放在 `(0, 1.0, 0)`，台地顶面 y = **1.0**；**门廊朝 -z**（来路侧）。

### 台地与挡土墙
- 台地实体：40 (x) × 26 (z)，顶面 y=1.0，厚 1.2（即 y −0.2…1.0），中心 `(0, 0.4, 2)`
- 挡土墙：沿台地 −z 边 + 左右两条边（+x / −x），厚 0.8，高从坡面到 y=1.0（够高即可，压进地面），
  墙顶加一条 0.15 m 厚的压顶石（`stone`），墙外侧可见
- 正面台阶：宽 8.0，5 级，每级高 0.28 × 深 0.45，从台地顶面下到坡面，居中 x=0

### 主堂（十字形，用盒体拼）
- 中殿（nave）：18.0 (x) × 20.0 (z)，墙高 **12.0**，中心 `(0, 6+1.0=7.0, 0)` 相对世界 → 用 church 局部 `(0, 6, 0)`
- 十字翼（transept）：26.0 (x) × 7.0 (z)，墙高 12.0，局部 `(0, 6, 0)`（与中殿同高，横向横穿）
- 山墙屋顶：中殿上方双坡，脊高 **15.0**（局部），沿 z 方向；两翼上方双坡，脊高 14.5，沿 x 方向
  → 用三角形棱柱（山墙剖面）实现，材质 `roofMat`
- 正立面（−z 面）中央山墙上方再叠一个小三角山花，脊高 16.0

### 鼓座与穹顶（12 边形）
- 鼓座：12 边形棱柱，外接圆**直径 13.0**（半径 6.5），高 **5.0**，局部 y 15.0 → 20.0
- 12 个拱窗：每边中央一个 1.2 宽 × 2.6 高的暗色矩形凹槽（用深色 box `stone` 压暗即可，别做布尔）
- 锥顶：12 边形锥，底半径 7.2，高 **5.5**，局部 y 20.0 → 25.5，材质 `roofMat`
- 顶上十字：两根 0.35 见方的梁组成（竖 2.0 高、横 1.2 宽），从 25.5 到 **27.5**，材质 `stone`

### 钟楼（8 边形，独立，右前方）
- 位置：局部 `(13.0, 0, -5.0)`（即世界 x=+13, z=−5），站在同一台地上
- 体身：8 边形棱柱，外接圆直径 7.0，分两段：
  - 实心段 y 1.0 → 12.0（世界）
  - 开敞钟室 y 12.0 → 16.0：8 根角柱（0.7×0.7×4.0）+ 顶部一圈 0.6 高的楣梁 + 8 个拱洞（拱用 3 段小 box 近似，或直接留空 + 楣梁）
- 锥顶：8 边形锥，底半径 4.2，高 **5.5**，16.0 → 21.5，材质 `roofMat`
- 顶上十字：到 **23.0**
- 钟楼与主堂之间用一段 0.8 厚的短墙连接（局部 x 8.5→11.5）

### 门廊（正面 −z，主堂中轴）
- 出挑平台：6.0 (x) × 1.8 (z) × 0.6 高，贴在中殿 −z 面（局部 z = −10.9）
- 两根立柱：0.6 × 5.0 × 0.6，局部 x=±2.2；拱楣：5.6 × 0.9 × 1.8，顶部 y=5.0
- 门洞：3.0 宽 × 4.2 高的深色 recess（深色 box 贴面，别布尔）

## 地形与山（同一轮必需，因为机位要贴地）

- 坡地仍 4°，`GroundY(z) = tan(4°)*z`，z=0 处 y=0（**保持现有解析式与 z=0 基准不变**）
- 坡地尺寸扩到 **160 (x) × 2 (y) × 200 (z)**，中心 z = **−30**，覆盖 z ∈ [−130, 70]
  （注意 `SlopeOriginY()` 里的 18 是旧半深，改尺寸时把半深一起改，别提上去）
- 石板路：从 z=−70 的 S 形走到台阶（z≈−12），段数 10–12，宽 3.2
- 三层远山：**重新调位置**，让它们在平视机位下形成上三分之一的天际线（见验收）

## 机位（重点是「贴地平视」，不是绕目标旋转）

相机高度 = 站点处地面 + **1.9 m**。新增辅助函数 `static Vector3 EyeAt(float x, float z) => new Vector3(x, GroundY(z) + 1.9f, z);`

1. `art_runtime`（揭示主镜头）：
   - 位置 `EyeAt(20f, -80f)`，看向 `(0f, 13.5f, 0f)`，FOV **36**
   - 期望俯角 ≈ 11.8°（由几何自然产生，不要硬写 euler 的 pitch）
2. `art_close`（近景，看门廊与像素细节）：
   - 位置 `EyeAt(5f, -22f)`，看向 `(0f, 6f, -10f)`，FOV 36（塔顶出画属正常）
3. `art_ground`（贴地量纹理密度）：保留现有 pitch 26 / d 7 的写法，位置改到新路径上 `EyeAt(0f, -14f)`

**ShotRig 若需要「位置 + 看向点」：** 给 `ShotRig` 加 `static ShotPose Look(string name, Vector3 eye, Vector3 target, float fov)`
（内部 `Quaternion.LookRotation(target - eye)`），其余保持原样。旧 `Make` 不要删。

## 角色

像素角色位置改到新路径上：`(0.5, GroundY(-62), -62)`，仍用射线吸附地面（保留原有吸附代码写法）。
`_ScaleY` 维持 1.75（1.75 m 高的人），**不要**为了凑屏高放大角色。

## 新增构图自检日志（验收要靠它，必须实现）

在 `Run()` 末尾（保存场景之前），把所有 `Church` 子节点 Renderer 的包围盒合并，投影到 viewport，
打印一行（数字保留 1 位小数）：

```
[SPIKE] frame church top=<px> bottom=<px> heightPct=<x.x> widthPct=<x.x> centerX=<px>
[SPIKE] frame pilgrim heightPct=<x.x>
[SPIKE] frame pitch=<x.x>
[SPIKE] frame ridge<1|2|3> linePct=<x.x>
```

- `top`/`bottom`/`centerX` 单位是 1080p 像素（top 从画面顶算）
- `heightPct` = (bottom−top)/1080×100
- `pitch` = 相机 forward 与水平面的夹角（度）
- `ridge line` = 每层山脊顶点在画面中的 y 百分比（取该层最高点投影）

## 验收标准（必须自己跑，把输出贴回来）

在仓库根 `/Users/lance/workspace/mistbell`：

```bash
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run \
  -logFile spike/logs/40_scene.log ; echo EXIT=$?
grep -E '\[SPIKE\] frame|ART DONE|error CS' spike/logs/40_scene.log
```

必须同时满足：

1. `EXIT=0`，且日志无 `error CS`
2. 出现 `[SPIKE] frame church` / `frame pilgrim` / `frame pitch` / `frame ridge` 四类行
3. `heightPct` ∈ **[45, 55]**（教堂含十字，占画面高）
4. `top` ∈ **[80, 240]** px 且 `bottom` ∈ **[820, 1000]** px
5. `centerX` ∈ **[900, 1020]** px
6. `frame pitch` ∈ **[10, 14]**
7. `pilgrim heightPct` ∈ **[10, 13]**
8. `ridge3 linePct` ∈ **[16, 30]**、`ridge1 linePct` ∈ **[30, 50]**
9. 教堂各部件齐全：`grep -c` 场景里存在 `Church/Drum`、`Church/Dome`、`Church/BellBody`、`Church/BellCone`、`Church/Nave`、`Church/Transept`、`Church/Portal`、`Terrace`、`RetainingWall`
10. 若数值不达标，**迭代改参数直到达标**（只改机位/山体/尺寸，不要靠放大角色凑数）

最后把上面命令的完整 `grep` 输出贴回你的回答里。

## 风格纪律

- 体块阶段允许是「积木」，但外轮廓必须一眼能认出是圣三一教堂：**十字形主堂 + 12 边鼓座锥顶 + 独立 8 边钟楼（右侧）**
- 保持所有 UV 以米计的盒体（`SpikeArt.Box`/`MeterUVBox`），不要用 `PrimitiveType.Cube`
- 不要改相机 clearColor / 雾 / 后期 / 光照；本轮只动几何与机位
