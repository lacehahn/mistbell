只做一件事：给这个 spike 加**可玩切片**——键盘走路 + 走到教堂门口按 E 弹介绍面板。不动美术（材质/山/雪/光）与机位表。

必读：`spike/project/Assets/Editor/SpikeScene.cs`（场景生成）、`Assets/Scripts/PilgrimAnimator.cs`、`Assets/Art/Shaders/PixelBillboard.shader`、`docs/00-vision.md`（教堂介绍文案口径）。

【要加 3 个脚本】
1) `Assets/Scripts/PlayerController.cs`
   - WASD / 方向键移动，速度 3.0 m/s，Shift 加速 5.0 m/s；地形高度用 Raycast 贴地（每帧一次，误差 ≤ 2 cm）。
   - 朝向按**相机相对**方向决定 4 向（前/后/左/右），驱动 billboard 的 `_Row`（down/left/right/up）与 6 帧循环（10 fps，移动时播放，静止时停在第 0 帧）。
   - 相机用**跟随**：保持与目标点偏移 `(0, 8, -22)`、看向玩家上方 3 m，平滑 lerp 0.12。
2) `Assets/Scripts/Interactable.cs`
   - 挂在教堂正门（台阶顶，世界坐标约 (0, y, −13)），触发半径 3.5 m。
   - 玩家进入半径时显示提示「按 E 查看」（角落小字）；按 E 打开面板。
3) `Assets/Scripts/InfoPanel.cs`
   - 全屏 Canvas：半透明底 + 像素边框面板（宽 720px），标题「Gergeti 圣三一教堂 / Gergeti Trinity Church」，正文 2 段（中文在上、英文在下），右下角「按 E / Esc 关闭」。
   - 文案（照抄，不要改写）：
     中文：「海拔 2,170 米，孤悬于卡兹别克山南麓。十四世纪，修士为避战乱把圣物带上山，此处从此成为高加索最孤独的教堂之一。风雪封山时，钟声先穿过云雾抵达谷地，人们才知道路还在。」
     英文：「At 2,170 m on the southern slope of Mount Kazbek. In the 14th century monks carried relics uphill to escape the wars, and it has been one of the Caucasus' loneliest churches ever since. When snow closes the pass, the bell crosses the mist before the church itself is seen.」

【交互模式】构建出的播放器：
- 带 `-shot` / `-shots` 参数时：**保持现有截图行为完全不变**（不能用玩家控制干扰机位）。
- 不带参数时：启用玩家控制 + 跟随相机 + UI（这是真正的可玩模式）。

【必须自己跑并给出证据】
1) 可玩模式的自动化验证：用参数 `-demo-walk 6`（持续 6 秒沿 +Z 前进、无输入）跑播放器，日志打印起点/终点距离与最终朝向行：
   "[PLAY] walk_start=(x,y,z) walk_end=(x,y,z) distance=<m> row=<n> fps_ok=<bool>"
   硬门槛：distance ≥ 15 m、贴地误差 ≤ 0.02 m、row 在 0..3。
2) 交互验证：参数 `-demo-interact` 打开面板并截图到 `Shots/ui_panel.png`，日志：
   "[UI] panel_open=true title_ok=true paragraphs=2 screenshot=Shots/ui_panel.png"
3) 原有截图链路（`spike/tools/capture_runtime.sh`）不得退化：跑一遍，measure_shot.py 指标仍满足 snow_ratio ≥ 0.40、magenta_px = 0、white_clip_ratio ≤ 0.06。

【顺序】SpikeScene.Run（需把玩家/UI/交互挂进场景）→ SpikeBuild.Run → 上述三条验证 → capture_runtime.sh → measure_shot.py
所有命令退出码 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改美术材质/山体/雪面/机位表/measure_shot.py。
