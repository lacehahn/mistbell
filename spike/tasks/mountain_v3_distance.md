只做一件事：把远山从"贴脸的硬纸板层叠"改成**远处的山谷背景**。不改教堂、角色、路面、地面材质、天空、相机。

必读：`spike/project/Assets/Editor/SpikeScene.cs` 的 `BuildMountains()`、`Assets/Art/Shaders/RidgeFog.shader`。

【现在的问题（三张证据都在画面里）】
1) **太近太高**：山脚 345/535/725 m、峰高 82/142/202 m → 山体像一堵墙顶到画面上沿，教堂背后没有山谷空间。
2) **水平层理线**：`layerThickness=14*(1+.35*sin(x*.0063))` 这类按世界高度分带的做法，让每座山都长出**横贯的白色/深色横线**，读成层叠硬纸板/胶合板。
3) **岩面是均匀麻点**，没有沟脊、没有崖壁方向性。

【要改】
1) **推远压低**：`distance = 700f + layer * 340f`（700/1040/1380），`peak = 120f + layer * 55f`；横向跨度保持。要求最高峰在画面中的位置 **top ≥ 0.18**（即不贴画面上沿），山脚占画面下沿之上 ≥ 0.55。
2) **删掉所有按世界 Y 分带的层理**（`layerThickness`/`strataPhase`/`frac(...)` 分带全部移除）。改用**垂直沟脊**：`rib = sin(x*.09 + noise*.8)` 与 `sin(x*.23+…)` 叠加，调制岩面明暗 ±12%；允许少量斜向断裂（slope 55°±15° 的三角面），但不许出现水平带状明暗。
3) 岩面明暗**跟随法线**（现有 lightFace 保留），加 ±8% triplanar 噪声，保持"石头的颗粒"，不要变成均匀麻点。
4) 大气透视：`_Aerial` 三层改 `0.30 / 0.62 / 0.86`，远层对比度再压 20%。

【自检打印】
"[MTN3] distance=700/1040/1380 peak=120/175/230 top_viewport_y=<f> foot_viewport_y=<f> horizontal_bands=0 ribs_per_100m=<n> aerial=0.30/0.62/0.86"
硬门槛：`top_viewport_y ≥ 0.18`（0=画面顶部）、`horizontal_bands == 0`、`ribs_per_100m ≥ 6`。
并跑 `python3 spike/tools/measure_shot.py`，不得退化：snow_ratio ≥ 0.55、white_clip_ratio ≤ 0.05、dark_ratio_lower 0.02–0.08、ground_shadow_gap ≥ 0.06、magenta_px = 0、hf_energy_lower 0.020–0.045。

【顺序】SpikeScene.Run → SpikeBuild.Run → capture_runtime.sh → measure_shot.py，四条退出码 0，同屏打印 FRAME md5 与 MEASURE。
不加包、不 commit、不改教堂/角色/路面/地面/天空/相机/measure_shot.py。
