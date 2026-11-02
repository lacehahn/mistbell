只做一件事：把上山路线从 1 段扩成 **3 段（折返）**，让"攀爬"有长度，并沿途加 2 个野外 POI。不改教堂/内部/远山贴图。

必读：`docs/03-flow.md`（Z01–Z03 的海拔/路长/视线）、`SpikeScene.cs` 的 `TerrainHeight()`/`BuildTerrain()`/`BuildStonePath()`/`BuildProps()`、`PoiDatabase`。

【路线】
现在只有一段：z 从 −88 到 −13 的直线坡（约 75 m）。
改成 3 段折返，总长约 **210 m**、总爬升 **26 m**：
- **Z01 山脚**：z −210 → −150，x 从 −18 折到 +10（缓坡 6°，碎石与枯草多）
- **Z02 林线折返**：z −150 → −95，x 从 +10 折回 −12（8°，两侧矮灌木）
- **Z03 高处草甸**：z −95 → −40，x 从 −12 缓到 0（10°，回望点可见山谷）
- 保留现有最后 30 m（−40 → −13）接入石阶与台地

【要改】
1) `TerrainHeight()`：沿折返中心线生成**路槽**（宽 5 m、比两侧低 0.25 m），两侧抬升为坡体；路槽以外保持雪原。
2) 路径与围栏：`BuildStonePath()` 按 3 段折返中心线重铺（仍是 2×3 石板/段，缝 4–8 cm）；围栏沿两侧跟随（台阶前 6 m 让位，保持现有规则）。
3) 相机雾：`MistReveal` 的距离基准仍是教堂（0,−9），不需要改；但**出门距离**从 88 m 变远，出生点改到 Z01 起点 `(x=−18, z=−205)`。
4) **2 个野外 POI**（数据驱动，沿用 `PoiData`；中文 4–6 行显式 \n、英文 1 段）：
   - `gergeti_p00` 回望点 · The Lookback（Z03 折返处，锚点路侧 2 m）：
     中文：「走到这里第一次能看见整条山谷。\n村子在脚下缩成几粒白点，\n云从对面山腰慢慢压过来——\n古人把这叫"进山"，从这里开始，路只剩向上。」
     英文：「This is the first place the whole valley opens up. The village shrinks to a few white dots below, and cloud slides down the far slope. The old word for this was *entering the mountain*: from here, the only way is up.」
   - `gergeti_p0a` 界石 · Boundary Stone（Z01→Z02 转折，锚点路侧 1.5 m）：
     中文：「路边的界石上刻着一道浅槽，\n是几百年来朝圣者用手摸出来的。\n摸过的人会在石缝里塞一枚小石子，\n意思是"我来过，也请让我回去"。」
     英文：「A shallow groove is worn into this boundary stone, rubbed smooth by centuries of pilgrims. Those who pass leave a pebble in the crack—*I was here, and let me come back down.*」
5) 出生点与提示：出生在 Z01 起点，屏幕中央一行 3 秒提示「W A S D 移动 · Shift 加速 · E 交互」（首次进入游戏即可见，之后不再出现）。

【自检打印】
"[ROUTE] segments=3 total_len_m=<f> climb_m=<f> spawn=(-18,-205) pois=5 slots=5 trench_w=5 trench_depth=0.25"
硬门槛：`total_len_m ≥ 200`、`climb_m ≥ 24`、`pois = 5`、`spawn` 与 Z01 起点一致。

【必须自己跑，且必须仍然走得到教堂】
1) 新增 `-demo-climb`：从 Z01 出生点沿折返路自动走到石阶（允许传送到各段起点再走 6 秒，模拟三段），每段打印 `[CLIMB] seg=<n> start=(x,y,z) end=(x,y,z) ground_err_max=<f>`，硬门槛每段 `ground_err_max ≤ 0.03`、且最终到台地 `on_deck=true`。
2) 回归：`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-cinema`、`-demo-reveal`、`-demo-enter-church`、`capture_runtime.sh`、`measure_shot.py`（magenta_px=0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂/内部/远山贴图/相机偏移/measure_shot.py。
