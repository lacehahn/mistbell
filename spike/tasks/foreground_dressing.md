只做一件事：给雪原前景加**低矮植被与碎石点缀**，填补空旷（现在前景 1/3 画面只有几块石头）。不改山体、教堂、角色、路面石板、天空。

必读：`spike/project/Assets/Editor/SpikeScene.cs` 的 `BuildProps()`、`Assets/Art/Textures/bush_sprite_*.png`（已有 8 张像素灌木）、`Assets/Art/Textures/grass.png`。

【要加】
1) **像素灌木片（billboard）**：沿路两侧与雪原散布 **24–36 丛**，每丛 1–3 张 sprite 交叉摆放（±35°），高度 0.45–0.9 m，扎根地形（raycast 贴地，落地误差 ≤ 2 cm）；颜色偏枯褐 `#7A6B52`（冬草），朝光面略暖。
2) **枯草簇**：36–48 簇，高度 0.18–0.35 m，贴地，颜色 `#8A7A5E`，只出现在雪面裸露斑块附近（离石头 0.5–1.5 m）。
3) **碎石带**：沿山脚与路缘 60–100 颗 0.08–0.22 m 的小石子，成串分布（每串 5–9 颗），颜色 `#6E6558`。
4) 所有点缀**不得进入**：教堂基座 18×18 m 区域、路面 2.2 m 宽范围、相机正前方 6 m 内。
5) 全部用 Point 采样、贴图无 mipmap 模糊（保持像素感），不投影（阴影关）。

【自检打印】
"[DRESS] bushes=<n> grass=<n> pebbles=<n> ground_snap_max_err_cm=<f> exclusion_violations=0"
硬门槛：bushes 24–36、grass 36–48、pebbles 60–100、ground_snap_max_err_cm ≤ 2、exclusion_violations = 0。
并跑 `python3 spike/tools/measure_shot.py`，不得退化：snow_ratio ≥ 0.55、white_clip_ratio ≤ 0.05、dark_ratio_lower 0.02–0.08、ground_shadow_gap ≥ 0.06、magenta_px = 0、hf_energy_lower 0.020–0.045。

【顺序】SpikeScene.Run → SpikeBuild.Run → capture_runtime.sh → measure_shot.py，四条退出码 0，同屏打印 FRAME md5 与 MEASURE。
不加包、不 commit、不改山体/教堂/角色/路面/天空/measure_shot.py。
