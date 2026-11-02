只做一件事：把像素远山的**等腰三角锥**改成**不对称山脊**。不改教堂、角色、路面、地面、石头、天空、相机。

必读：`spike/project/Assets/Editor/SpikeScene.cs` 里生成 `MtnLayer_{1,2,3}.png` 的函数、`art-ref/sheet-v2-church-hd2d.jpg` 封面远山。

【问题】
现在每座山都是**等腰三角形**：左右坡度对称、宽度雷同、单峰无次生脊，像一排帐篷。

【要改（只改生成像素山的脊线部分）】
1) **不对称**：每个峰的左坡度与右坡度比在 **1 : 1.6 ~ 1 : 2.8** 之间随机（或反向），峰顶允许偏移，不居中。
2) **次生山脊**：主峰两侧各挂 1–2 条**肩脊**（低于主峰 25–45% 高度，水平偏移 0.3–0.6 倍峰宽），让轮廓出现"多级台阶"。
3) **峰宽差异**：峰底宽 60–260 px 随机（现在几乎一致）。
4) **岩面三阶明暗**：脊线以下 0–30% 阴影、30–70% 中间调、70–100% 亮面；按 `|斜率|` 决定明暗（陡面暗、缓面亮），不要整座山一个平面色。
5) **雪帽**：只覆盖脊线下方 8–36 px，并按斜率裁剪（陡面雪少）；边缘 2 px 硬边 + 随机锯齿 1–3 px。
6) 三层不共用同一随机种子（各自独立）。

【自检打印】
"[MTN5] peaks_per_layer=<l1>/<l2>/<l3> asymmetry_ratio=1.6..2.8 width_px=60..260 spurs=1..2 shading_levels=3 seed_per_layer=distinct"
并跑 `python3 spike/tools/measure_shot.py`。
硬门槛：每层 peaks 9–14、asymmetry 全在 1.6–2.8、magenta_px = 0、snow_ratio ≥ 0.55、white_clip_ratio ≤ 0.05。

【硬性禁止】不许出现左右完全对称的峰（自检里统计 `symmetric_peaks` 必须 = 0）。

【顺序】SpikeScene.Run → SpikeBuild.Run → capture_runtime.sh → measure_shot.py，退出码全 0，同屏打印 FRAME md5 与 MEASURE。
不加包、不 commit、不改教堂/角色/路面/地面/石头/天空/相机/measure_shot.py。
