# 派遣任务：教堂正面立面（山墙 / 门廊 / 台阶 / 窗）+ 3/4 机位

## 现状（必须知道）

教堂现在是 **Blender 建的**：`spike/tools/build_church.py`（bpy，米制，Z 轴朝上，正面朝 **-Y**）→ `spike/blend/gergeti_church.blend`
→ 导出 `spike/export/gergeti_church.fbx` → 手动复制到 `spike/project/Assets/Art/Church/gergeti_church.fbx`
→ Unity `SpikeScene.Run` 里 `ImportChurch()` 导入并按名字分材质（stone / roof / wood）。

主堂尺寸已对：总宽 34 m、总高 27 m（含十字）、进深 24 m。**体量对了，但正面是一面平板墙**：
只有一个 2 m 的小拱门，没有山墙、没有门廊、没有台阶、没有立面窗；钟楼钟室只有 1 个拱洞（应为 8 面开敞）。

用户参考图（格鲁吉亚 Gergeti 圣三一教堂）要求：正面有**突出的山墙 + 双柱拱形门廊 + 5 级台阶 + 窄高拱窗**，
钟楼**顶层 8 面开敞**。

## 只允许改这些文件

- `spike/tools/build_church.py`（主战场）
- `spike/project/Assets/Editor/SpikeScene.cs`（只改机位表 + LogFrame 增加 porch 一行日志）
- 产物：`spike/blend/gergeti_church.blend`、`spike/blend/views/*.png`、`spike/export/gergeti_church.fbx`、
  以及复制到 `spike/project/Assets/Art/Church/gergeti_church.fbx`
- **不要**改材质/贴图/光照/雾/后期/角色/山体/地形；不要改 docs

## 要做的几何（Blender 侧，单位米，坐标沿用脚本现有基准：中殿中心 (0,-1)、正面 -Y、入口地面 (0,-6.5,0)）

1. **正面山墙（Gable）**：主堂前立面顶部加三角形山花，压在檐口之上、穹顶之前。
   - 檐口高度沿用当前主墙顶；山尖比檐口高 **3.5 m**，山墙厚 = 墙厚，材质 `stone`
   - 山墙两坡加 `roof` 材质的屋面薄板（厚 0.25，出檐 0.4）
2. **门廊（Porch）**：正面中轴，出挑 **1.8 m**、宽 **6.0 m**、高 **5.5 m**
   - 两根角柱 0.7×0.7，柱头压顶 0.15
   - 拱楣：门廊正面开口用**拱形**（复用脚本里现成的 `arch()` 开口/布尔流程），开口宽 3.2、拱顶高 4.6
   - 门洞：3.0 宽 × 4.2 高的内凹 0.6 m（材质 `wood` 门板）
3. **台阶（Steps）**：宽 **8.0 m**、5 级、每级高 0.28 × 深 0.45，从台地顶面接门廊地面（Z 从台地顶到入口地面）
4. **立面窗**（拱顶窄高窗，深 0.25 的内凹 + `wood` 底）：
   - 中殿正面左右各 1 个：1.0 宽 × 2.4 高，中心高度约 6.5 m
   - 山墙上 1 个：0.9 宽 × 1.6 高，居中在 11 m 左右
   - 两个侧墙各 2 个：0.9 宽 × 2.2 高，均匀分布
5. **钟楼钟室 8 面开敞**：当前只有 1 个洞 → 改成 8 个面各 1 个拱洞（宽 2.2、高 3.2、拱顶），洞与洞之间留角柱；
   钟室顶部保留一圈楣梁与锥顶
6. 约束：总宽 34 / 总高 27（含十字）/ 进深 24 **不许变**；三角面总数 **< 60000**；所有面片 UV 必须存在
   （`--verify` 会断言）；命名沿用现有风格，命名里含 `porch` / `steps` / `gable` / `window` 便于 Unity 分材质。

## Unity 侧

- `SpikeScene.Run` 机位改成 **3/4 视角**（相机在教堂右前方，x 偏移 +25~30，仍贴地 `EyeAt(x,z)`），
  保持 `frame church heightPct ∈ [45,55]` 与 `frame pitch ∈ [10,14]`
- `art_close` 重定位：站在台阶前的地面上（离台地正面约 8 m，x 偏 +6），看向门廊中部（高 3 m），
  pitch 5–8°，FOV 36。**当前 art_close 直接怼在墙里，必须修掉**
- `LogFrame` 增一行：
  `[SPIKE] frame porch heightPct=<x.x> centerX=<px>`（取名字含 `porch` 的所有 Renderer 合并包围盒投影）

## 验收（必须自己跑，把输出贴回回答）

```bash
cd /Users/lance/workspace/mistbell
blender -b --python spike/tools/build_church.py ; echo BUILD=$?
blender -b --python spike/tools/build_church.py -- --verify ; echo VERIFY=$?
cp spike/export/gergeti_church.fbx spike/project/Assets/Art/Church/gergeti_church.fbx
ls -l spike/project/Assets/Art/Church/gergeti_church.fbx
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project \
  -executeMethod SpikeScene.Run -logFile spike/logs/50_scene.log ; echo EXIT=$?
grep -E '\[SPIKE\] frame|ART DONE|error CS' spike/logs/50_scene.log
```

必须同时满足：

1. `BUILD=0`、`VERIFY=0`（脚本自带三角面/UV/米制往返断言通过）
2. 日志出现 `[SPIKE] frame church`、`frame porch`、`frame pitch`，且 `error CS` 为 0
3. `church heightPct ∈ [45,55]`、`pitch ∈ [10,14]`、`porch heightPct ∈ [8,30]`、`porch centerX ∈ [760,1160]`
4. 三角面数 **> 50000 且 < 60000**（脚本会打印，贴出来）
5. `spike/blend/views/front.png` 重新渲染且**正视图能看出：山墙三角 + 门廊双柱拱 + 5 级台阶 + 立面窗**
6. 钟楼钟室 8 个拱洞（正视/侧视能数出来）

数值不达标就迭代到达标；**不要**靠放大角色、缩教堂来凑 `heightPct`（那会推翻已验收的尺寸）。
