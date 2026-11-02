只做一件事：把"平平的打光"改成**低角度暖阳 + 冷蓝环境光 + 长投影**的黄昏雪山光。不改模型、材质贴图、相机、山体、雪面几何。

必读：`spike/project/Assets/Editor/SpikeScene.cs` 里的 `Sun` 灯光与 `BuildProfile()`（后处理 Volume）、`spike/tools/measure_shot.py`。

【要改】
1) **太阳**：仰角 20°→**14°**、方位保持左前（−40°）；颜色 `#FFCE9B`；强度 1.6→**2.4**；阴影 120 m→150 m、分辨率 2048→**4096**、soft shadows 保持开。
2) **环境光**（Trilight）压暗偏冷：sky `#5E7794`、equator `#4E627C`、ground `#38414F`（现在偏亮偏灰，把暖阳的对比吃掉了）。
3) **后处理**：postExposure −0.15→**+0.10**、contrast 18→**24**、saturation −4→**+6**；whiteBalance 偏移向暖 +8；Vignette 强度 +0.05。
4) **火盆光**：两个火盆的 Point Light 半径 8→**12 m**、强度 3→**4.5**，并加**闪烁**（正弦噪声 ±12%，周期 0.15–0.4 s，用 Time.time 驱动；batchmode 截图时用固定相位，避免每帧随机导致不可复现）。
5) **窗内暖光**：自发光强度提 30%，让窗户在暗部更亮。

【自检打印】
"[LIGHT2] sun elev=14 az=-40 color=#FFCE9B intensity=2.4 shadow=150/4096 ambient=#5E7794/#4E627C/#38414F exposure=+0.10 contrast=24 sat=+6 brazier_r=12 i=4.5 flicker=on"
并跑 `python3 spike/tools/measure_shot.py`。
硬门槛：ground_shadow_gap ≥ **0.08**、warm_px ≥ **20000**、magenta_px = 0、snow_ratio ≥ 0.50、white_clip_ratio ≤ 0.06、hf_energy_lower 0.020–0.045。

【顺序】SpikeScene.Run → SpikeBuild.Run → capture_runtime.sh → measure_shot.py，退出码全 0，同屏打印 FRAME md5 与 MEASURE。
不加包、不 commit、不改相机/模型/贴图/山体/雪面几何/measure_shot.py。
