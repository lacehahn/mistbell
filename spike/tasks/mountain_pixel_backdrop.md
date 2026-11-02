只做一件事：把 3D 山脊换成**三层手绘风格的像素山背景**（HD-2D 的做法是"手绘远景 + 3D 前景"，程序化 3D 山脊在这套光照下永远像布褶）。不改教堂、角色、路面、地面、天空、相机。

必读：`art-ref/sheet-v2-church-hd2d.jpg`（封面远山）、`spike/project/Assets/Art/Textures/{Snow_Pixel,Rock_Pixel}.png`、`spike/tools/measure_shot.py`。

【做法】
1) 程序化生成 **3 张 1024×512 像素山图**（存入 `Assets/Art/Textures/MtnLayer_{1,2,3}.png`，Point、无压缩、mipmap off）：
   - 脊线：**ridged multifractal**（|noise| 取反再相乘 4 octave），得到**尖锐峰 + 鞍部**，不是正弦褶；
   - 每层峰数 9–14、峰高差异 ≥ 15%（标准差 ≥ 18 px）；
   - 岩面：3 阶灰蓝（#4A5666 / #5E6B7C / #74828F），沿脊线向下明暗递减；
   - 雪：只在脊线下方 0–40 px 且朝上面（用脊线切线的法向判断）覆盖 #EAF1F7，边缘用 2 px 硬边 + 少量锯齿；
   - 层间色偏移：L1 最暗最实、L2 向 #8FA0B4 混 35%、L3 向 #C3D2E0 混 60%。
2) 用 **3 个面向相机的 Quad** 贴在 900 / 1300 / 1800 m 处（宽度按 FOV 算，保证横向铺满不留缝），透明通道 cutout，不投影、不接收雾。
3) 高度：L1 顶在视口 **0.30–0.34**、L2 0.26–0.30、L3 0.22–0.26；底边压到地形之下（≥ 地形高度），不允许出现悬空下沿。
4) **保留 3D 山脊对象但禁用**（`SetActive(false)`），便于回退。

【自检打印】
"[MTN4] layers=3 tex=1024x512 peaks=9..14 ridge_std_px>=18 top_viewport_y=L1:<f>/L2:<f>/L3:<f> quads_width_m=<n> 3d_ridges_disabled=true"
并跑 `python3 spike/tools/measure_shot.py`。
硬门槛：三层 top_viewport_y 落在上面区间、magenta_px = 0、snow_ratio ≥ 0.55、white_clip_ratio ≤ 0.05、ground_shadow_gap ≥ 0.06。

【顺序】SpikeScene.Run → SpikeBuild.Run → capture_runtime.sh → measure_shot.py，四条退出码 0，同屏打印 FRAME md5 与 MEASURE。
不加包、不 commit、不改教堂/角色/路面/地面/天空/相机/measure_shot.py。
