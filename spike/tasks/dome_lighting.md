只做一件事：给**鼓座内壁与穹顶**补光，让抬头时不再是一团灰。不改外观、不改玩法、不改路线。

必读：`Shots/interior_lookup.png`（鼓座偏灰、锥顶是一块暗盘）、`ChurchInteriorBuilder.cs`、`ChurchInterior.cs`。

【要改】
1) **鼓座补光**：在 y=10.5 处加 **2 盏向上打的暖点光**（位置 ±1.8 m、范围 8 m、强度 1.6、色 #FFE0B8），照亮鼓座内壁与穹顶锥面。
2) **窗光**：12 个拱窗内侧各加一个小面光替代（0.4 m 的发光片，色 #CFE2F5、强度 1.2），让窗洞亮起来（不新增实时光源，避免 12 盏开销）。
3) **穹顶锥面材质**：从 #6E675D 提到 #8A8177，并给 0.05 的轻微自发光，避免死黑。
4) **顶部开口**：孔径 0.5 → 0.8 m，并在开口上方放一块**天空色发光面**（#BFD7EA），读成"透光孔"而不是"黑洞"。
5) 与 F 键抬头联动：抬头时把这两盏补光的强度从 0 提到 1.6（0.4 s 渐入/渐出），平时保持 0.35，避免室内常态过亮。

【自检打印】
"[DOME_LIGHT] uplights=2 intensity=1.6 windows_emissive=12 cone_color=#8A8177 aperture=0.8 headroom_luma=<f>"
硬门槛：抬头时 `headroom_luma ≥ 0.28`（画面中心 60% 区域），且 `interior_luma`（站立视角）仍在 0.18–0.45。

【必须自己跑】
`-demo-enter-church`（重出 interior_entry / interior_inside / interior_lookup）。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-cinema`、`-demo-reveal`、`capture_runtime.sh`、`measure_shot.py`（magenta_px=0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改外观/玩法/路线/相机/measure_shot.py。
