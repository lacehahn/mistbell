只做一件事：修远山的"纸锥 + 黑色楔形阴影"。不改教堂、角色、雪面、相机、天空。

必读：生成 `MtnLayer_{1,2,3}.png` 的函数、`art-ref/target-path-hd2d.jpg`。

【诊断（从当前帧读出来的三个硬伤）】
1) **黑色楔形**：每座山的暗面是大块近黑楔形，且方向完全一致 → 读成折纸，而不是受光的岩石。
2) **峰高差过大**：峰与峰高差太夸张，像一排锯齿。
3) **层间没有大气**：三层山的明度几乎一样实，没有"越远越淡"的纵深。

【要改】
1) **暗部不再近黑**：岩面最暗值抬到 `#5A6876`（现在接近 #2A3038），暗部面积 ≤ 山体面积 25%；暗→亮用 3 阶过渡，不要两阶硬切。
2) **峰高收窄**：同层峰高标准差 ≤ 峰高的 20%（现在是它的一倍多）；峰顶不要全是尖角，允许 30% 的峰是圆钝的。
3) **层间大气**：L2 整体向 `#9FB0C2` 混 35%，L3 向 `#C6D3E0` 混 60%；并在每层底部 25% 高度叠一条向上渐隐的雾带（也让山脚沉进地面）。
4) **雪帽收窄**：雪只覆盖脊线下方 6–26 px，陡坡少雪、缓坡多雪（按局部斜率），不要整座山头一片白。
5) **整体降高 15%**：让教堂成为画面主体，山退成背景。

【自检打印】
"[MTN7] peak_height_std_pct=<f> dark_area_pct=<f> darkest=#5A6876 layer_mix=35/60 haze_band_pct=25 snow_cap_px=6..26 height_scale=0.85"
硬门槛：peak_height_std_pct ≤ 20、dark_area_pct ≤ 25、darkest 不低于 #5A6876（用十六进制比较）、height_scale = 0.85。

【测量】跑 `python3 spike/tools/measure_shot.py`，另外用 Python 统计山体区域内 V<0.30 的像素占比，必须 ≤ 0.08：
"MOUNTAIN_DARK V_lt_030=<f>（目标 ≤0.08）"
（山体区域：y 在画面 12%–55%、排除教堂包围盒 x∈[0.33W,0.66W] 且 y>0.30H）

【顺序】SpikeScene.Run → SpikeBuild.Run → capture_runtime.sh → measure_shot.py，退出码全 0，同屏打印 FRAME md5 与 MEASURE。
不加包、不 commit、不改教堂/角色/雪面/相机/天空/measure_shot.py。
