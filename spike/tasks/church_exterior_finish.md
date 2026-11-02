只做一件事：完成"教堂外观收尾"的四项——底座扩大、远山填满背景、人物拉大、到达底座时的仰视过场。不改教堂模型本身、不改 POI 文案、不改音频。

必读：`SpikeScene.cs`（台地/底座/远山 Quad 生成）、`PlayerController.cs`（跟随相机）、`MistReveal.cs`（远山 Quad 与雾）、`Shots/reveal_{80,50,20}.png`。

【T1 底座扩大（用户实测：石阶超出了底座）】
- 现在 9 级石阶宽 5.0 m，比台地边缘还外凸。把台地/底座外扩：**四周各再外扩 1.5 m**（总 34→37 × 24→27），保证石阶两侧各留 ≥1.2 m 石台，石阶前缘不超出底座前沿。
- 挡土墙随之重排（段数允许变化），墙顶仍不可站立。
- 打印：`[BASE] plinth=<w>x<d> stair_side_margin=<m> stair_front_margin=<m>`
- 硬门槛：`stair_side_margin ≥ 1.2`、`stair_front_margin ≥ 1.0`。

【T2 远山填满背景（用户：多数时候卡兹别克山没填满背景）】
- 现在三层像素山 Quad 宽 1049/1516/2099 m。把宽度按"最广机位全屏铺满"重新算：**每层投影宽度 ≥ 屏幕宽度 × 1.25**，并把山体整体抬高 35%（`height_scale 0.85 → 1.15`），使画面左右两侧不留白天空。
- 打印：`[MTN8] layer_width_m=<l1>/<l2>/<l3> screen_fill_pct=<f> top_y=<l1>/<l2>/<l3>`
- 硬门槛：三层 `screen_fill_pct ≥ 1.25`、最远层 `top_y ≤ 0.30`（填满上半部）。

【T3 镜头拉近（用户：人主体要更大）】
- 跟随相机 `(0, 5.5, −14)` → **`(0, 4.4, −10.5)`**，看向玩家上方 3.2 m。
- 打印：`[CAM] offset=(0,4.4,-10.5) pilgrim_px=<n>`
- 硬门槛：静止站立时主角屏高 **130–175 px**（当前约 105）。

【T4 到达底座时的仰视过场（一次性）】
- 触发：玩家首次进入 `z ≤ −15` 且 `|x| ≤ 12`（底座正前方）。
- 过场 3.4 s：相机从当前位下降到 `player.y + 1.1`、俯仰改为**仰视 +14°**、FOV 42，保持 1.2 s 让玩家看到**整座教堂**（顶不切、底不切），再用 1.0 s 缓回原跟随镜头。
- 期间冻结玩家输入，结束后恢复；只触发一次（`cinemaDone`）。
- 打印：`[CINEMA] enter z=<f> hold=1.2 restore=1.0 church_top=<f> church_bottom=<f> screenshot=Shots/cinema_lookup.png`
- 硬门槛：`church_top ∈ [0.04, 0.22]`、`church_bottom ≥ 0.80`（整座教堂在画面内）、结束时相机回到跟随偏移误差 ≤ 0.3 m。

【必须自己跑】
1) `-demo-cinema`：出生后自动走向底座触发过场并截图 `Shots/cinema_lookup.png`（新增演示参数）。
2) `-demo-reveal` 三张图重出，检查 T2 的山是否铺满。
3) 回归：`-demo-walk 6`（distance ≥ 15、贴地 ≤ 0.02）、`-demo-walk-stairs`（on_deck=true）、`-demo-walk-to-tower`（reached=true、blocks ≤ 2）、`capture_runtime.sh` exit 0、`measure_shot.py` magenta_px = 0。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂模型/POI 文案/音频/measure_shot.py。
