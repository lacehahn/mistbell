只做一件事：修**近距离观感**——玩家走到教堂门口时（d=20 m）现在是"纸板教堂"：墙是一块米色平面、屋顶被切掉、火盆是白球、石头影子是黑星。不改山体、雪原大形、路面走向。

必读：`Shots/reveal_20.png`（现状）、`Assets/Scripts/PlayerController.cs`（跟随相机）、`Assets/Editor/SpikeScene.cs`（教堂材质/火盆/接触阴影）、`art-ref/sheet-v2-church-hd2d.jpg`。

【要改 5 点】
1) **跟随相机**：偏移 `(0, 8, −22)` → **`(0, 5.5, −14)`**，看向玩家上方 **3.5 m**；俯角自然上仰，保证教堂塔顶不切。要求：d=20 m 时教堂顶在画面 y ∈ [0.06, 0.20]（顶部不被裁）。
2) **教堂石材近景**：`M_Stone` 的贴图 tiling 现在太大，近看是一块纯色。把 `Stone_Pixel` 的 tiling 提高到 **每 1.8 m 一个 tile**（现在约 4.8 m），并保留 masonry 明暗差；近景必须能数出砌块（≥ 8 块跨 5 m 宽度）。
3) **火盆**：点光半径 12 → **7 m**、强度 4.5 → **2.2**；发光体的自发光/泛光减半，不许出现直径 > 1.5 m 的白球。
4) **石头接触阴影**：alpha 0.10 → **0.05**，并且**只在朝阳一侧偏 0.15 m**（现在的黑星是阴影贴片被拉成星形）。若仍呈星形，直接删掉贴片，只靠实时阴影。
5) **围栏与路面**：栏杆底面与石板顶面间距 ≤ 2 cm（现在悬空），柱子底座加 0.05 m 下沉。

【自检打印】
"[CLOSEUP] cam_offset=(0,5.5,-14) church_top_y=<f> stone_tile_m=1.8 masonry_per_5m=<n> brazier_r=7 brazier_i=2.2 contact_alpha=0.05 rail_gap_m=<f>"
硬门槛：church_top_y ∈ [0.06, 0.20]、masonry_per_5m ≥ 8、rail_gap_m ≤ 0.02。

【必须自己跑】
`-demo-reveal`（重出 reveal_80/50/20 三张）+ `-demo-walk 6`（distance ≥ 15 m、贴地 ≤ 0.02）+ `capture_runtime.sh`（exit 0）+ `measure_shot.py`（magenta_px = 0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改山体/雪原大形/路面走向/机位表/measure_shot.py。
