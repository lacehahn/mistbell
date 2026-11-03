只做一件事：**地平线与天空**——雪原和天空之间现在是一条笔直刀切的线（放大图：雪面到 y≈0.72 突然变灰蓝），天空还有大块色带和一条左上角的黑色竖条。不改教堂/玩法/相机/室内。

必读：`Shots/art_runtime.png`，放大左半边（雪原 → 地平线 → 天空）。三个问题：
1) 地平线是硬边：远景雪原没有任何空气透视，直接撞到天空；
2) 天空有色带（5bit 量化 + 云太大块）；
3) 左上角 x≈100–130（1920 宽）有一条**竖向暗条**，从画面上缘一直压到云里。

【要改】
1) **地平线雾**：在雪原地形的最远端（距相机 >600 m 区域）叠加朝向天空色的垂直雾，`0 → 0.85` 随距离渐增；雾色取**当前天空地平线附近的实际颜色**（不要写死 #C9D7E4，取 sky gradient 上 horizon 的颜色）。
2) **天空色带**：抖动从 4×4 提到 **8×8 Bayer**，量化步数 5bit → **6bit**（噪声幅度 0.5 → 0.35，避免噪点）。
3) **云更碎**：云带尺度 ×0.6、覆盖 0.85 → 0.7，让云是碎絮而不是整块。
4) **竖条**：定位那条暗条的来源（大概率是天空穹顶的 UV 接缝或某层山 Quad 的边缘），修掉；如果是穹顶接缝，把 UV 环绕改为 world-direction 采样。

【自检打印】
"[HORIZON] haze_far=<f> haze_color=#<hex> sky_steps=<n> dither=<n>x<n> cloud_scale=<f> stripe_max_dev=<f> horizon_step_max=<f> sky_band_pct=<f>"
硬门槛：
- `stripe_max_dev ≤ 0.03`（天空区每一列的均值与左右邻列之差的最大值）
- `horizon_step_max ≤ 0.035`（沿竖直方向跨过地平线时相邻像素亮度差的最大值）
- `sky_band_pct ≤ 0.05`（天空区相邻像素亮度跳变 >0.02 的比例）
- `magenta_px = 0`、`snow_ratio ≥ 0.55`、`white_clip_ratio ≤ 0.05`

【必须自己跑】
`capture_runtime.sh`（3 张）、`-demo-cinema`、`-demo-reveal`（`gate=pass`）。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-enter-church`、`-demo-journal`、`-demo-trail 10`、`measure_shot.py`。
⚠️ 演示一律用 `bash spike/tools/run_demos.sh "<参数>"`（内含 caffeinate + `open -n -a`）；直接执行二进制会因窗口非激活静默卡住。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂/玩法/相机/室内/measure_shot.py。
