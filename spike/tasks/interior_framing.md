只做一件事：修室内**构图与光柱**（相机太高像玩偶屋、光柱是两大块不透明楔形、吊灯只有空环、圣像屏是素箱子）。不改外观、不改台地/远山、不改 Blender 模型几何（只允许改 Unity 侧相机/材质/亮度与物件缩放，可加小构件）。

必读：`Shots/interior_inside.png`（现状）、`PlayerController.cs`（室内相机段）、`SpikeScene.cs`（室内灯光/光柱/吊灯/圣像屏）。

【要改 4 点】
1) **室内相机改为"人眼高度"**：偏移 `(0, 3.0, −7.0)` → **`(0, 1.75, −5.2)`**，看向玩家上方 **+2.2 m**（略微仰视圣像屏），FOV 32 保持。
   - 硬约束不变：相机 y ≥ 地板 + 1.5、离墙 ≥ 0.35、不得穿入长椅/柱。
   - 目标构图：玩家在画面下 1/3（脚踩画面 0.88 附近），圣像屏在中上、吊灯在画面上 1/5。
2) **光柱**：现在是两大块 alpha 0.22 的实心楔形。改为 **additive 加法混合 + 0.06–0.10 alpha**、宽度减半、每条独立随机偏移与轻微抖动（0.3 Hz），并用软边（中心 1.0 → 边缘 0 的径向渐变）。数量 4 → 3。
3) **吊灯**：环上补 **8 支小蜡烛**（0.06 半径圆柱 + 暖点光各 1，强度 0.35、半径 1.2），环半径 1.2 保持，高度降到 y=7.2。
4) **圣像屏**：素箱子上加 **3 个拱形画框**（0.9×1.6 m，深色框 + 暖色内芯），并把中间一扇做成略亮的"圣门"。

【自检打印】
"[INT2] cam_in=(0,1.75,-5.2) player_screen_y=<f> iconostasis_screen_y=<f> chandelier_screen_y=<f> shafts=3 shaft_alpha=0.08 additive=true chandelier_candles=8 frames=3"
硬门槛：`player_screen_y ∈ [0.80, 0.95]`、`iconostasis_screen_y ∈ [0.28, 0.50]`、`chandelier_screen_y ∈ [0.12, 0.26]`、`shafts=3`。

【必须自己跑】
`-demo-enter-church` 重出 `interior_entry.png`/`interior_inside.png`；`-demo-poi gergeti_p04` 重出 `interior_poi_p04.png`。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-cinema`、`capture_runtime.sh`、`measure_shot.py`（magenta_px=0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改外观/台地/远山/POI 文案/Blender 模型/measure_shot.py。
