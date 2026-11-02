只做一件事：补室内的**天花板与吊灯可见性 + 地面石板 + 长椅木材质**。不改外观、不改相机偏移、不改 POI 文案、不改 Blender 模型几何（只允许 Unity 侧新增/调整物件与材质）。

必读：`Shots/interior_inside.png`（现状：无天花板、吊灯在画面外 chandelier_screen_y=-1.42、地面平色、长椅纯色木箱）、`SpikeScene.cs` 的室内段。

【要改 4 点】
1) **天花板**：现在抬头是墙顶/天空。给 nave + transept 加**木质天花板**（单面朝下的四坡/双坡板面，最低点 y=11.6、屋脊 y=12.0），颜色 #4A3B30，带 5 条横向梁（0.25×0.3 m，间距 2 m）。
2) **吊灯位置**：现在 y=7.2、在 FOV 32 的人眼视角之外。改为 **y=4.6、z=−4.5、x=0**（挂在玩家前方 3 m、高 4.6 m），保证在 `interior_inside` 里可见：屏幕 y ∈ [0.12, 0.30]。吊灯本体：铁环 r=0.9（0.06 粗）+ 8 支蜡烛（0.05 r，0.22 高）+ 3 根吊索到天花板。
3) **地面石板**：室内地面改铺 **1.2×0.8 m 石板**（共 300–450 块，缝 3 cm，颜色 #8F8579，明暗随机 ±7%），并在中轴留一条 1.6 m 宽深色石带（#6E655B）直通圣像屏。
4) **长椅木材**：6 排长椅改 `M_Wood`（#6B4A32），椅面加 0.06 m 高亮边（#7A573C），椅脚深一档（#4E3624）。

【自检打印】
"[INT3] ceiling=true beams=5 chandelier=(0,4.6,-4.5) chandelier_screen_y=<f> floor_slabs=<n> aisle=true pew_material=M_Wood"
硬门槛：`chandelier_screen_y ∈ [0.12, 0.30]`、`floor_slabs ≥ 300`、`beams = 5`。

【必须自己跑】
`-demo-enter-church` 重出 `interior_entry.png`、`interior_inside.png`；`-demo-poi gergeti_p04` 重出 `interior_poi_p04.png`。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-cinema`、`-demo-reveal`、`capture_runtime.sh`、`measure_shot.py`（magenta_px=0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改外观/相机偏移/POI 文案/Blender 模型/measure_shot.py。
