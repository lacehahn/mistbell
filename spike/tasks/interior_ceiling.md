只做一件事：让**室内天花板与吊灯真正出现在画面里**。不改外观、不改路线、不改 POI 文案、不改 Blender 模型。

必读：`Shots/interior_inside.png`（现状：抬头看不到天花板、吊灯在画面外）、`PlayerController.cs` 室内相机段、`ChurchInterior.cs`、`ChurchInteriorBuilder.cs`。

【现状问题】
1) 天花板建在 11.6 m，人眼相机（1.75 m，看向 +2.2 m，FOV 32）**垂直视野只覆盖约 ±16°**，11.6 m 高在 5 m 距离处是 63° 仰角 → 永远看不到。
2) 吊灯在 (0,4.6,−4.5)，投影指标算出 `chandelier_screen_y=-8.68`（**公式本身错了**，很可能是拿 local 坐标或背后点投影），视觉上也不在画面里。

【要改】
1) **室内相机**：FOV 32 → **38**，看向点 `玩家 + 3.2 m`（原 2.2），仍保持人眼高度 1.75 m；出室内恢复 FOV 35 / 4.4/3.2。
2) **天花板降到 8.6 m**（nave/transept 统一，屋脊 9.0 m），保留 5 道梁；在穹顶正下方留一个 **1.6 m 见方的开口**（让人知道上面是鼓座，不是平顶）。
3) **吊灯**：移到 `(0, 4.0, −3.2)`，铁环 r=0.9、8 支蜡烛、3 根吊索接到天花板（索长按 8.6−4.0 算）。
4) **修正投影公式**：`chandelier_screen_y` 必须用 `cam.WorldToViewportPoint(worldPosition)` 的 **world** 位置，再取 `1 - vp.y`；打印时同时给出 `vp` 原值便于自查。

【自检打印】
"[INT4] cam_in=(0,1.75,-5.2) fov_in=38 look=3.2 chandelier=(0,4.0,-3.2) chandelier_vp=<x>,<y> chandelier_screen_y=<f> ceiling_y=8.6 ceiling_visible=<bool> ceiling_top_pct=<f>"
硬门槛：`chandelier_screen_y ∈ [0.10, 0.30]`、`ceiling_visible = true`（画面顶部 12% 内出现天花板像素）、`ceiling_top_pct ≥ 0.06`（天花板至少占画面 6%）。

【必须自己跑】
`-demo-enter-church` 重出 `interior_entry.png`、`interior_inside.png`；`-demo-poi gergeti_p04` 重出 `interior_poi_p04.png`。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-cinema`、`-demo-reveal`、`capture_runtime.sh`、`measure_shot.py`（magenta_px=0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改外观/路线/POI 文案/Blender 模型/measure_shot.py。
