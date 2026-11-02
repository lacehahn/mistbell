只做一件事：修**室内相机穿地/穿墙**、把室内照亮到能看清家具、并修正室内物件比例。不改外观、不改远山、不改 Blender 模型几何（只允许改 Unity 侧材质/灯光/相机与物件缩放）。

必读：`Shots/interior_inside.png`（现状：画面下半部是穿到地板下的斜切暗块）、`PlayerController.cs`（跟随相机/防穿墙）、`SpikeScene.cs`（室内灯光与物件）、`Assets/Scripts/InteriorLighting.cs`（若存在）。

【问题清单（从截图读出）】
1) 相机在室内压低到地板以下，导致下半屏是地面剖面。
2) 室内除烛台外几乎全黑；长椅/祭坛/吊灯/圣像屏看不见。
3) 烛台粗大得像柱子；室内石材像素块过大（约 1.5 m 一块）。

【要改】
1) **室内相机**：进入室内（玩家 x∈[−4.5,4.5]、z∈[−9,6]）后切换室内 rig：
   - 偏移 `(0, 3.0, −7.0)`、看向玩家上方 2.4 m、FOV 32；
   - **硬约束**：相机 y ≥ 室内地面 + 1.7 m；与墙/柱/椅的最小间距 0.35 m（沿用 SphereCast，但把最小拉近距离改为 1.8 m，且**禁止**在水平方向拉近到 1.8 m 以下时继续下压 y）；
   - 出室内恢复室外 rig `(0,4.4,−10.5)` FOV 35。
2) **室内照明**：
   - 环境光：室内时 `RenderSettings.ambientSkyColor = #5A4A3C`、`ambientEquatorColor = #43382E`、`ambientGroundColor = #2A2320`（暖而暗）；
   - 加 1 盏"中殿补光"：位置 (0, 6.5, −2)、范围 16 m、强度 0.85、颜色 #FFD9A8、无阴影；
   - 烛台点光半径 4→**2.6 m**、强度 1.6→**1.2**；吊灯补 1 盏半径 9 m、强度 1.0；
   - 窗光柱保持 4 条，但透明度 0.35→**0.22**。
3) **比例**：烛台圆柱半径 0.25→**0.12**、高度 1.4→**1.1**；室内石材 tiling 从 1.8 m → **0.9 m**（近看砌块更小更细）。
4) **确认可见性**：长椅 6、祭坛 1、圣像屏 1、吊灯 1 必须在 `interior_inside` 这张图里出现在中景范围内；若它们的 y 位置在地板下，修正到地面高度。

【自检打印】
"[INT_FIX] cam_in=(0,3.0,-7.0) fov_in=32 cam_min_y=<f> cam_wall_gap=<f> cam_below_floor=false"
"[INT_FIX] interior_luma=<f> furniture_visible=pews/altar/iconostasis/chandelier"

【必须自己跑】
`-demo-enter-church` 重出 `interior_entry.png`、`interior_inside.png`；`-demo-poi gergeti_p04` 重出 `interior_poi_p04.png`。
硬门槛：
- `cam_below_floor=false`（相机任何帧都不得低于地板 + 1.5 m）
- `interior_luma ≥ 0.18`（中殿区域平均亮度，用截图中心 40% 计算）
- `furniture_visible` 四项全部 true（用遮挡/投影判断或直接检查屏幕内是否存在对应对象）
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-cinema`、`capture_runtime.sh`、`measure_shot.py`（magenta_px=0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改外观机位表/远山/雪面/Blender 模型/measure_shot.py。
