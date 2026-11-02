只做一件事：修**底座扩大后留下的空白台面**，并把远山的"撕纸感"压下去。不改室内、不改相机偏移、不改 POI。

必读：`Shots/interior_entry.png`（36 m 宽的空白雪台面）、`Shots/art_runtime.png`、`SpikeScene.cs` 里台地/挡土墙/远山生成。

【问题 1：台地太空】
现在底座 37×27 m，石阶两侧各留 15.5 m 空地，整片白得像一张桌子，教堂像摆在上面。
【要改】
1) 台地顶面**不是平的**：沿四周做 0.15–0.35 m 的雪堆起伏（噪声驱动），靠近墙根更高。
2) 台地**加内容**（不新增贴图，用现有材质）：
   - 正面石阶两侧各加 **2 个石墩/界石**（0.7×0.9×0.7，顶部积雪）
   - 台地左右各加 **1 段矮墙/栏杆残段**（长 6 m、高 0.5 m，带雪盖），把大空面切断
   - 台地四角各加 **1 个石堆**（3–5 块 0.4–0.8 m 石头成簇）
   - 沿墙根一圈**雪裙**（0.4–0.7 m 高，连续）
3) 挡土墙墙顶（coping）宽度 0.45 → **0.70 m**，并把墙顶做成**向外倾斜 5°**（雪不易积在平面上，视觉也更实）。

【问题 2：远山像撕纸】
雪帽现在是整条白色带，边缘锯齿规则、三层几乎同亮度，读成撕开的纸。
【要改】
1) 雪帽厚度按坡度变化：陡面 2–6 px、缓面 10–22 px（现在统一 6–26 px）。
2) 雪帽边缘加**双层锯齿**（大锯齿 6–12 px 周期 + 小锯齿 2–4 px），不再等间距。
3) 三层明度必须有明显梯度：L1 与 L3 的平均亮度差 **≥ 0.18**（现在几乎一样），L2 居中。
4) 山体底部 12% 叠一条**地雾带**（白色 → 透明渐变），把山脚与雪原接起来，消除硬切线。

【自检打印】
"[TERRACE2] mounds=<n> bollards=4 walls=2 rockpiles=4 skirt_segments=<n> coping_m=0.70 tilt_deg=5"
"[MTN9] snowcap_px=2..22 double_jagged=true layer_luma=L1:<f>/L2:<f>/L3:<f> gradient=<f> haze_band_pct=12"
硬门槛：`gradient ≥ 0.18`、`mounds ≥ 12`、`skirt_segments ≥ 20`、硬切线消除（山脚处亮度差的水平方向跳变 ≤ 0.04）。

【必须自己跑】
`-demo-enter-church`（重出 interior_entry.png，检查台面）、`capture_runtime.sh`（重出 art_runtime.png，检查远山）、`-demo-reveal`（三张）。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-cinema`、`measure_shot.py`（magenta_px=0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改室内/相机偏移/POI 文案/Blender 模型/measure_shot.py。
