只做一件事：修**上不了台阶**的 bug，并把移动从"匀速滑行"改成有加减速与坡度阻力。只动移动相关代码。

必读：`Assets/Scripts/PlayerController.cs`、`SpikeScene.cs` 里生成台地石阶的部分（`[TERRACE] steps=9 step_rise=0.18 step_run=0.34`）、`docs/07-gameplay-slice.md`。

【BUG 1：玩家无法登上台阶（用户实测报告）】
现象：走到 9 级石阶前无法上去。
根因假设：地面判定是**单次竖直 raycast**，只认地形；台阶高 0.18 m 是独立碰撞体，玩家被"高度墙"挡住或直接穿过。
修法（两条都做）：
1) **台阶步升（step-up）**：每帧在**上坡方向**前探 0.35 m 处向下 raycast；若命中面比当前地面高 **≤ 0.28 m**，则把玩家 y 抬到该面（插值 12 m/s，不要瞬移）；若更高则视为墙，禁止通过。
2) 在石阶上方铺一条**不可见斜向碰撞坡**（宽 5.0 m、从路终点到台地面，倾角按 9×0.18/踏深计算），作为兜底：即使步升失效也能平滑上去；此坡 `Renderer.enabled = false`，只在玩家层生效。

【BUG 2：出生点离教堂太近，开场就响钟】
出生点后移到 `z = −88`（原 −82），保证开场距教堂 > 70 m，钟声只在真正接近时响一次。

【体验 3：位移手感】
- 加入加减速：目标速度由输入决定，实际速度用**加速 12 m/s²、减速 18 m/s²**逼近。
- 上坡减速：坡度 > 8° 时速度 ×0.82，> 18° 时 ×0.6；下坡 ×1.05。
- 转向：朝向用最短角插值，角速度 540°/s，不要瞬切。

【自检打印】
"[MOVE] step_up_max=0.28 ramp=true accel=12 decel=18 spawn_z=-88"
"[MOVE] stair_test_start_y=<f> end_y=<f> deck_y=<f> climbed=<bool> distance=<m>"
以及 `[PLAY] walk_start=... walk_end=... distance=... row=...`（原格式保留）

【必须自己跑，并给出证据】
1) 新参数 `-demo-walk-stairs`：玩家从路终点（z≈−16）朝教堂方向直走 8 秒；日志必须 `climbed=true` 且 `end_y ≥ deck_y − 0.1`、`distance ≥ 6 m`。
2) `-demo-walk 6`：distance ≥ 15 m、贴地误差 ≤ 0.02。
3) `-demo-reveal`：三张图仍在，`bell_played_at_d` 在 44–46 且只触发一次。
4) `capture_runtime.sh` exit 0；`measure_shot.py` 的 `magenta_px = 0`。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改美术材质/山体/雪面/相机机位表/POI 文案/measure_shot.py。
