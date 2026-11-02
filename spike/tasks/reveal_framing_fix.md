只做一件事：修 **reveal 机位回归**（教堂顶被切出画面），并顺手修三个硬度瑕疵（雪帽变白线、主角贴图发糊、围栏错位）。不改室内、不改 POI、不改 Blender 模型。

必读：`spike/logs/terrace2_verified_matrix.log`、`PlayerController.cs` 的 reveal 机位段、`MountainBackdrop.cs`、`PixelBillboard.shader`、`SpikeScene.cs` 的围栏生成。

【BUG 1：reveal 三张图把教堂顶切掉】
现状：`-demo-reveal` 退出码 1，断言要求 `church_top ∈ [0.06, 0.20]`，实测 **-0.705661**（教堂顶在画面上方之外）。
原因：底座扩大（34×24 → 37×27）与相机偏移改动后，reveal 用的固定机位没有重算，距离/仰角不足。
修法：重算 d=80/50/20 m 三档机位——保持"朝教堂中轴、机位在路面上、仰视"的意图，但必须让**整栋教堂入画**：
- 目标机位由"教堂包围盒 + FOV"反算：画面里教堂顶 ≥ **0.06**、底 ≤ **0.95**、左右各留 ≥ 8% 边距。
- 三档机位沿路径取点（z = −88 / −58 / −29 附近，x 跟随路径），高度取地面 +1.9 m，朝向教堂中心 + 轻微上仰。
- 断言改成新口径：`church_top ∈ [0.04, 0.20]` 且 `church_bottom ∈ [0.80, 0.97]`。

【瑕疵 2：山脊雪帽变成"白线"】
`[MTN9] snowcap_px=2..22` 但视觉上是一根根白线（太细太亮，且沿脊线只有 1–3 px 实际宽度）。
改：雪帽**实际最小厚度 5 px**（缓坡 12–24 px），并且**雪帽下缘加 2–4 px 的蓝灰过渡**（#C6D5E3）而不是直接切到岩色。

【瑕疵 3：主角放大到 150 px 发糊】
`PixelBillboard` 的贴图采样改为 **point（最近邻）**，关闭 mipmap/aniso；材质 `_MainTex` 的 filterMode 在运行时强制 Point。

【瑕疵 4：围栏在石阶附近错位】
围栏沿路取点的偏移与石阶中线未对齐，导致栏杆在台阶处断开/穿模。
改：围栏在 z ∈ [−18, −12] 区间直接跳过（让台阶自己收口），并在台阶两侧各加 1 根端柱（高 1.05 m）。

【自检打印】
"[REVEAL2] d=80 top=<f> bottom=<f> | d=50 top=<f> bottom=<f> | d=20 top=<f> bottom=<f>"
"[FIX3] snowcap_min_px=5 edge_soft_px=2..4 sprite_filter=Point fence_skip_z=-18..-12 end_posts=2"

【必须自己跑】
`-demo-reveal`（必须 **exit 0**，三张图重出，且三档 top/bottom 都落在新口径内）
`-demo-enter-church`（重出 interior_entry/interior_inside）
`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-cinema`、`capture_runtime.sh`、`measure_shot.py`（magenta_px=0）
全部退出码 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改室内光照/POI 文案/Blender 模型/measure_shot.py。
