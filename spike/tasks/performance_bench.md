只做一件事：**性能实测与优化**——给出 1920×1080 下的帧率、draw call、三角面、显存占用基线，并把明显的浪费修掉。不改美术内容、不改玩法。

必读：`SpikeScene.cs`、`PlayerController.cs`、`Shots/`、`spike/tools/measure_shot.py`。

【要做的测量（必须打印真数）】
新增 `-bench` 参数：加载完成后在**三个位置**各采样 300 帧，输出：
"[BENCH] pos=<z01|terrace|interior> fps_avg=<f> fps_p1=<f> drawcalls=<n> tris=<n> vram_mb=<f>"
- 位置：Z01 出生点、台地前、室内中殿
- fps 用 300 帧真实 `Time.unscaledDeltaTime` 均值与 1% low
- drawcalls / tris 用 `UnityEngine.Rendering.ProfilerRecorder`（`Draw Calls Count`、`Triangles Count`）
- vram 用 `ProfilerRecorder` 的 `Texture Memory`（拿不到就打印 -1 并说明）

【优化（只在测量发现问题时做）】
1) 若 `drawcalls > 400`：把静态美术（石头、围栏、石板、雪裙、构件）按材质**合批**（同材质合并 mesh 或开 GPU Instancing），目标 `drawcalls ≤ 250`。
2) 若 `tris > 350k`：远山三层 Quad 与天空穹顶不需要阴影 pass（`shadowCastingMode = Off` 已开）；检查室内石板 218 块是否各占一个 drawcall，改成 3–5 个合并 mesh。
3) 若 `fps_avg < 55`：检查是否有 `Camera.Render` 之外的重复渲染、是否有每帧 `FindObjectsByType`（把 Start 里的一次性查找缓存成字段）。
4) **不改**：模型面数、贴图分辨率、光照参数、后期强度。

【自检打印】
"[BENCH_OK] before=drawcalls:<n>/tris:<n>/fps:<f> after=drawcalls:<n>/tris:<n>/fps:<f>"
硬门槛：室内与室外 `fps_avg ≥ 55`、`drawcalls ≤ 250`、不出现 magenta 像素。

【必须自己跑】
`-bench`（三次采样）、`-demo-enter-church`、`-demo-reveal`、`-demo-walk-to-tower`、`capture_runtime.sh`、`measure_shot.py`（magenta_px=0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改美术内容/玩法/相机机位/measure_shot.py。
