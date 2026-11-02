只做一件事：让教堂、石头、围栏**真正长在雪里**（现在都像"摆"在雪面上）。不改山体、角色、相机、天空。

必读：`spike/project/Assets/Editor/SpikeScene.cs` 的 `ImportChurch()`、`BuildProps()`、`BuildTerrain()`。

【要加】
1) **教堂基座雪裙**：沿教堂包围盒外缘（28.2×23.1 m）做一圈**雪堆**：宽 1.2–2.2 m、高 0.25–0.6 m 的柔和白色体块，**贴地并向上收窄**，与地形无缝；背风面（−X 侧）更高（0.5–0.8 m）。
2) **墙根积雪**：教堂墙体下沿 0.6 m 内，用**朝上的白色薄片**（0.02 m 厚）沿墙面顺时针包一圈，遮住"墙插进雪"的硬线。
3) **门前清雪**：正门前 3×4 m 区域把雪压平（高度 −0.06 m），露出台阶前 2 级。
4) **石头接触**：每块石头下方加一个**软阴影椭圆贴片**（0.9×1.3 倍石头尺寸、alpha 0.22、贴地 +0.01 m），让石头不再"浮"。
5) **围栏柱脚**：每根柱底加一个小雪堆（r 0.18–0.28 m、高 0.1–0.16 m）。

【自检打印】
"[CONTACT] church_drift_segments=<n> wall_snow_strips=<n> entry_cleared=<bool> rock_shadows=<n> post_drifts=<n> float_errors=0"
硬门槛：church_drift_segments ≥ 24、wall_snow_strips ≥ 4、rock_shadows = 石头总数、post_drifts = 柱子总数、float_errors = 0（每个新增体块都要 raycast 贴地，误差 ≤ 3 cm）。
并跑 `python3 spike/tools/measure_shot.py`，不得退化：snow_ratio ≥ 0.55、white_clip_ratio ≤ 0.05、dark_ratio_lower 0.02–0.08、ground_shadow_gap ≥ 0.06、magenta_px = 0。

【顺序】SpikeScene.Run → SpikeBuild.Run → capture_runtime.sh → measure_shot.py，退出码全 0，同屏打印 FRAME md5 与 MEASURE。
不加包、不 commit、不改山体/角色/相机/天空/measure_shot.py。
