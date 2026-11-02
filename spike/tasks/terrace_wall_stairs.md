只做一件事：给教堂加**石砌挡土墙 + 正面石阶**，把"平地上的房子"变成"台地上的圣地"。不改山体、角色、雪面材质、相机、天空。

必读：`art-ref/target-path-hd2d.jpg`（目标参考：台地 + 挡土墙 + 宽石阶）、`spike/project/Assets/Editor/SpikeScene.cs` 的 `BuildStonePath()` 与教堂导入处（`ImportChurch`）。

【现状】教堂坐在 34×24×1.2 m 的台地上，但台地边缘直接切进雪面——没有挡土墙、没有台阶，读成"摆上去的模型"。参考图里台地是**石头砌的墙围起来**，正面一段宽石阶上去。

【要加】
1) **挡土墙**：沿台地外缘（约 34×24 m）做一圈石墙，**高 1.4–1.8 m、厚 0.45 m**，墙面用 `stone_ashlar` 贴图（与教堂同族材质），墙顶加 **0.12 m 雪盖**；墙身按 3–5 m 一段留 2–3 cm 缝（砌块感）。
2) **正面石阶**：从现有石板路的终点（z≈−13.25）到台地顶面，**8–10 级**，每级高 0.16–0.20 m、踏面深 0.34 m、宽 5.0 m；两侧各一段 0.5 m 宽的矮墙（高 0.9 m）。
3) **平台收口**：台地顶面与教堂基座之间补一圈 0.25 m 高的雪堆，遮住接缝。
4) 墙与台阶全部 raycast 贴地，误差 ≤ 3 cm；不许悬空或穿模。

【自检打印】
"[TERRACE] wall_segments=<n> wall_h=<m> steps=<n> step_rise=<m> step_run=<m> stair_w=<m> snow_cap=true float_errors=0"
硬门槛：wall_segments ≥ 12、wall_h 1.4–1.8、steps 8–10、stair_w 5.0±0.1、float_errors = 0。
并跑 `python3 spike/tools/measure_shot.py`，不得退化：snow_ratio ≥ 0.55、white_clip_ratio ≤ 0.05、dark_ratio_lower 0.02–0.10、ground_shadow_gap ≥ 0.06、magenta_px = 0、hf_energy_lower 0.020–0.045。

【顺序】SpikeScene.Run → SpikeBuild.Run → capture_runtime.sh → measure_shot.py，退出码全 0，同屏打印 FRAME md5 与 MEASURE。
不加包、不 commit、不改山体/角色/相机/天空/measure_shot.py。
