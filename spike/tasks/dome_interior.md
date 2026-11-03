只做一件事：把室内**穹顶开口做成真的鼓座内壁**（现在抬头看到的是一块平面，像贴片）。不改外观、不改玩法、不改路线。

必读：`Shots/interior_lookup.png`（抬头视角：中央开口是一块米色平面）、`ChurchInteriorBuilder.cs`、`art-ref/sheet-v2-church-hd2d.jpg`（鼓座/穹顶剖面）。

【要改】
1) 在天花板开口正上方补**鼓座内壁**：12 边形环（内径 r=3.2、外径 r=4.2、高 3.6 m，从 y=8.6 到 12.2），内表面朝下，材质同石材（#8C8579），带 12 条竖向肋（0.18×0.18 m）。
2) 鼓座内壁开 **12 个拱窗洞**（每个 0.7×1.4 m，真实开洞），从窗洞透出**冷光**（4 条细光柱，alpha 0.10，加法混合）。
3) 顶部封**穹顶内表面**：锥面（底半径 3.2、高 2.4 m，从 y=12.2 起），内表面朝下，深色石质 #6E675D，并在锥顶留 0.5 m 圆孔（透一点天光）。
4) 抬头（按 F）时，`look` 目标从 6.4 改到 **10.2**，让鼓座与穹顶都在画面里。

【自检打印】
"[DOME] drum_sides=12 drum_h=3.6 windows=12 ribs=12 cone_h=2.4 aperture=0.5 look_target=10.2"
"[LOOKUP] ceiling_visible=<bool> dome_visible=<bool> ceiling_pct=<f>"
硬门槛：`windows=12`、`ribs=12`、`dome_visible=true`、抬头时 `ceiling_pct ≥ 0.35`。

【必须自己跑】
`-demo-enter-church`（重出 interior_entry / interior_inside / interior_lookup）。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-cinema`、`-demo-reveal`、`capture_runtime.sh`、`measure_shot.py`（magenta_px=0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改外观/玩法/路线/相机机位/measure_shot.py。
