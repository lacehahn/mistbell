只做一件事：给三层像素远山**加真实细节**——现在它们是"灰蓝纸片 + 锯齿白边"，像儿童画。不改教堂、角色、雪面、相机、天空。

必读：`spike/project/Assets/Editor/SpikeScene.cs` 里生成 `MtnLayer_{1,2,3}.png` 的函数、`art-ref/target-path-hd2d.jpg`（真实参考：山体有岩层、雪沟、山脚林线、大气雾带）。

【要加（只改生成像素山的绘制部分）】
1) **岩层纹理**：脊线以下的岩面按高度画 **3–5 条不连续岩层带**（每层 6–18 px 高，亮度差 ±8%），带的边缘**沿脊线角度倾斜**（不能水平），并在带内加 2–4 px 随机缺口。
2) **雪沟**：从脊线向下画 **8–20 条雪沟**（每条宽 2–6 px、长 20–90 px，沿坡向，末端渐隐），让雪不是一整条白边，而是"顺坡流下的雪"。
3) **山脚林线**：最下层山脚（y 从底边向上 0–12%）加**深色针叶林剪影**（每 4–9 px 一个 3–7 px 高的三角，颜色 #26313A，L1 明显、L2 淡、L3 省略），把山和雪原连起来。
4) **大气雾带**：每层在图的下 30% 叠一层水平渐变（向 `#C9D6E3` 混 0%→45%），让山脚"沉进雾里"。
5) 三层分别处理，L3 最淡（雾最多），L1 最实。

【自检打印】
"[MTN6] strata_bands=3..5 per_layer snow_gullies=<l1>/<l2>/<l3> treeline=yes/yes/no haze_bottom_pct=30"
并跑 `python3 spike/tools/measure_shot.py`。
硬门槛：snow_ratio ≥ 0.55、white_clip_ratio ≤ 0.05、magenta_px = 0、hf_energy_lower 0.020–0.045（细节增加但不能过噪）。

【顺序】SpikeScene.Run → SpikeBuild.Run → capture_runtime.sh → measure_shot.py，退出码全 0，同屏打印 FRAME md5 与 MEASURE。
不加包、不 commit、不改教堂/角色/雪面/相机/天空/measure_shot.py。
