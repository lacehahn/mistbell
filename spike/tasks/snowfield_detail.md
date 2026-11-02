只做一件事：把"一整块平白"的雪原做成**有坡形、有风纹、有色彩层次的雪地**。不改教堂、山体、角色、路面石板、相机、天空。

必读：`Assets/Art/Shaders/TerrainHaze.shader`、`spike/project/Assets/Editor/SpikeScene.cs` 的 `TerrainHeight()` / `BuildTerrain()`、`spike/tools/measure_shot.py`。

【问题】画面 45% 是雪原，现在几乎一个明度、没有坡、没有风纹、没有冷暖变化，读成一张白纸。

【要改】
1) **中尺度雪丘**：8–80 m 范围内加 drifts，长轴沿风向 ±30°，振幅 0.15–0.5 m，波长 6–18 m（多方向正弦 + perlin 混合，不要规则条纹）。
2) **风蚀脊（sastrugi）**：细长硬脊，宽 0.3–0.8 m、长 3–8 m、高 0.08–0.15 m，成组沿风向排列；**只铺在距相机 45 m 内**（远景不铺，避免噪）。
3) **色彩分层**：凹处偏蓝 `#B9CBDE`、坡面 `#E4EDF5`、脊顶 `#F6FAFD`；由坡度与局部高度决定，不许整片同色。
4) **裸露斑块**：路边与石头 1–3 m 内露出深灰碎石斑（每块 0.5–2 m），占雪面面积 3–6%。
5) **细节 tiling 随距离分级**：距相机 <25 m 用 0.6 m 尺度，25–60 m 用 1.6 m，>60 m 用 3.0 m（避免远处高频噪点）。

【自检打印】
"[SNOW2] drifts=<n> sastrugi=<n> exposure_patches=<n> tiling_near=0.6 mid=1.6 far=3.0 shadow_tint=#B9CBDE"
并跑 `python3 spike/tools/measure_shot.py`。
硬门槛：snow_ratio ≥ 0.55、white_clip_ratio ≤ 0.05、dark_ratio_lower 0.02–0.10、ground_shadow_gap ≥ 0.06、magenta_px = 0、**hf_energy_lower 必须留在 0.020–0.045**（过噪比平更需要避免）。

【顺序】SpikeScene.Run → SpikeBuild.Run → capture_runtime.sh → measure_shot.py，退出码全 0，同屏打印 FRAME md5 与 MEASURE。
不加包、不 commit、不改教堂/山体/角色/路面/相机/天空/measure_shot.py。
