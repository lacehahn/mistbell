# 任务：祭坛改成真正的 3D 模型 + 手绘贴图（HD-2D）

## 现状（已核实）
- 场景里的祭坛**不是模型**，是 5 个 `PrimitiveType.Cube` 叠出来的：
  `Interior_V2AltarBody / _Top / _Cloth / _Drape` + `Interior_V2Niche*`，材质是纯色 Lit。
- 已有起点：`spike/tools/build_altar.py`（Blender 程序化）能出 `spike/export/gergeti_altar.fbx`，
  当前 4740 tris、bbox `2.24 × 2.09 × 1.01`（2.09 含 1.10 m 十字），形体是
  "底座两阶 + 坛体 + 四角壁柱 + 三个框龛 + 台面下压条 + 台面 + 台布 + 十字 + 3 烛台"。
  形体基本对，但：**没有贴图（纯色）**、框龛偏浅、台布没有布料感、十字偏粗。
- 场景侧：`spike/project/Assets/Editor/ChurchInteriorBuilder.cs` 里 `Interior_Altar` 这个名字
  被碰撞测试引用（`PlayerController.TestInteriorObstacles`），**改名会挂演示**。

## 目标
把祭坛做成"有手绘贴图的真 3D 模型"，在 HD-2D 观感里近看能读出石材砌缝、刻线、金饰，
远看轮廓和现在一致。不追求写实，追求"手绘像素质感 + 清晰形体"。

## 要做的事
1. **模型精修**（可改 `build_altar.py`，Blender 5.2.3 在 `/opt/homebrew/bin/blender`）
   - 坛体 2.08 × 0.83 × 0.85 m（不含底座与台面），底座两阶高 0.11 m，台面厚 0.07 m 外挑 0.06 m。
   - 三个框龛：内板进深 0.05 m（现在是 0.012，太浅读不出凹陷），框条宽 0.045 m、出挑 0.03 m，
     龛顶加 0.03 m 石刻挑檐；龛内背板单独一个材质槽（要能贴更暗的图）。
   - 台布：前沿垂坠 0.34 m，竖向 3 道褶（每道宽 0.05 m、起伏 0.02 m），褶要有厚度不是贴片。
   - 十字：竖杆 0.11 × 1.10 × 0.05，横杆 0.34 × 0.10，四端倒角 0.008，中心小圆凸。
   - 烛台 3 座，间距 0.44 m，盘径 0.10 m。
   - 全部倒角 ≥ 2 段；总 tris **≤ 9,000**；导出 `spike/export/gergeti_altar.fbx`。
2. **UV + 手绘贴图**
   - Smart UV / 手动展 UV，texel 密度约 **128 px/m**，贴图 **1024×1024** 一张。
   - 贴图必须走项目已有手绘管线：参考 `spike/art-prompts/staged/` 里的石头/木料样张
     （`stone_big_a.png`、`stone_cornice.png` 等），生成 `spike/art-prompts/staged/altar_stone.png`，
     再由现成脚本进 Unity（不要新造一套导入流程）。
   - 要求：石材砌缝每 0.35–0.60 m 一条、有 3 档明度差（同色相灰阶）、框条顶面比立面亮 8–12%、
     龛内背板比立面暗 15–20%、十字与烛台用偏暖的石色/木色区分。
3. **接入场景**
   - 用模型替换那 5 个 Cube（保留对象名 `Interior_Altar` 给碰撞测试用；内部子物体随便命名）。
   - 材质：一个主材质（手绘贴图）+ 一个"暗部"材质给龛内背板，共 2 个材质槽，不要更多。
   - 位置与朝向：与原祭坛完全重合（原坐标从 `ChurchInteriorBuilder` 里取），**龛面朝会众**。
   - 碰撞：保留原来的 `Interior_Altar` 碰撞体尺寸（BoxCollider），不要让玩家穿模。
4. **不要动**：室外材质/光照、相机、其他室内物件、演示脚本的判定逻辑（除了必要的名字对齐）。

## 验收（必须自己跑并把数字贴出来）
1. `[ALTAR_MODEL]` 日志：`tris`、`materials`、`bbox`、`texel_px_per_m`。
2. 门槛：`tris ≤ 9000`、`materials ≤ 2`、`bbox 2.20±0.1 × 1.05±0.08 × 0.95±0.1`（不含十字）、`texel_px_per_m 110–150`。
3. 截图两张：`altar_closeup.png`（1.2 m 距离正对龛面）、`interior_inside.png`（原机位）。
   肉眼确认：①三个龛读得出凹陷 ②框条有受光/背光差 ③台布有褶 ④十字与烛台不是纯色块 ⑤贴图不糊、不重复。
4. `-demo-enter-church` 全绿（`[INT_PASS] obstacles=True` 不得回退）。
5. 性能：室内 `drawcall ≤ 260`、`fps ≥ 200`。
