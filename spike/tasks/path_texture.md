# 任务：石板路换成用户生成的手绘贴图（下段裸石 / 上段雪压）

## 素材（已入库）
- `spike/art-prompts/staged/path_stone_a.jpg` — 1254²，**裸石大石板**，中调 `(124,112,100)` 偏暖灰，
  三阶明确、缝干净；接缝差 4.5 / 内部 1.9
- `spike/art-prompts/staged/path_stone_snow.jpg` — 1254²，**雪压石**，冷灰 `(146,152,164)`，
  雪盖 ≈30%、带踩实暗斑；接缝差 5.3(左右) / 9.6(上下) / 内部 3.8
- `spike/art-prompts/staged/path_stone_steps.jpg` — 1280×853，阶石（**只允许用在台地前那 9 级石阶**）

## 目标
把现在的 `M_Paving`（`#8C8378` 纯色 + 程序化石板 + 一条白线）换成真贴图，并且**按海拔做两段过渡**：
- 低处（`y < 台地底 - 10 m`）：裸石 `path_stone_a`
- 高处（`y > 台地底 - 2 m`）：雪压石 `path_stone_snow`
- 中间 8 m 做线性混合（越往上越白），正好对应"往山上走雪越来越厚"

## 要做的
1. **导入贴图**：两张各裁成 1024²（或直接 1254²，Unity 侧设为 Point? **不**——这种手绘平铺用 Bilinear + Repeat，
   关闭 mipmap 会闪，保留 mipmap，`aniso 4`），sRGB。
2. **材质**：`M_Paving` 的 `_BaseMap` 改成混合用图；世界投影尺度 **2.4 m 一铺**（石板约 0.5–0.8 m）。
   - 由于两张图左右已接近可拼、上下有接缝，使用**世界坐标投影 + 两档尺度叠加（±37° 旋转）**掩盖接缝；
   - 别用 mesh UV（现在的路面是 3×2 块 Box，UV 会拉长）。
3. **海拔混合**：在 shader 里按 `worldPos.y` 做 mix（或给材质加 `_SnowLine` 参数），过渡带宽 8 m。
4. **去掉那条白线**：现在路中间有一条贯穿的白色亮带（截图里像公路标线）。查明它是什么对象
   （`[PATH_WHITE] name= material=`），是独立的雪/边条网格就删掉，是材质问题就修掉。
5. **石阶**：`path_stone_steps` 只用在台地前 9 级台阶的踏面，竖向不要平铺（裁一条踏面循环即可）。

## 验收
- `[PATH_TEX] a=path_stone_a snow=path_stone_snow scale_m=2.4 blend_band=8 mipmap=true aniso=4`；
- `[PATH_WHITE] removed=true/false`；
- 截图：`path_closeup.png`（路面近景 3 m）、`cam_play_1080.png`（玩法机位，看整条路）；
- 目视：路面能看出石板形状、上段明显更白、**没有白线**、没有肉眼可见的重复网格；
- `-demo-walk 6`、`-demo-enter-church`、`-demo-reveal` 全绿、`bad=0`；
- 室外 `fps ≥ 200`、台地 `drawcall ≤ 330`。

## 硬约束
- 不动室内、祭坛、人物（1.5 m）、玩法相机参数、围栏、雾。
