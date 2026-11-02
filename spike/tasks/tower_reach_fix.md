只做一件事：修**走不到钟楼**——玩家在台地上被挡住，接近不了钟楼（POI `gergeti_p02` 锚点 10.5, y, −2，半径 3.0 m）。只动碰撞与调试输出。

必读：`Assets/Scripts/PlayerController.cs`、`Assets/Scripts/Interactable.cs`、`SpikeScene.cs` 里教堂碰撞体（`church_colliders=3`）与挡土墙（28 段）的生成部分。

【要做的三件】
1) **加阻挡诊断**：移动被挡住时（期望位移与实到位移差 > 0.02 m），打印一次（同帧去重、每秒最多 3 条）：
   `[BLOCK] by=<collider 名字> at=(x,y,z) normal=<Vector3>`
   这样能直接看出是墙、教堂还是钟楼碰撞体挡的。
2) **修行走路径**：让玩家能从台阶顶 → 台地面 → 钟楼脚下 **全程畅通**，直到进入钟楼 POI 的 3.0 m 半径。
   - 钟楼碰撞体：用 **CapsuleCollider（r=2.2, h=13）** 或 4×4 的 Box，**不要用整块 MeshCollider**（现在很可能把塔基周围一起围住）。
   - 检查 28 段挡土墙是否**侵入台地内部**（墙心线应在台地外缘外侧 0.25 m）；若有侵入，整体外移。
   - 教堂主堂碰撞体边距不要超过实测墙体外扩 0.15 m。
3) **POI 可达性**：钟楼 POI 锚点若落在塔体内，挪到塔前 2.4 m 处（z 方向朝广场一侧），保证玩家站在塔前就能进半径。

【自检打印】
"[TOWER] collider=Capsule r=2.2 h=13 wall_intrusion=<m> poi_anchor=(x,y,z) poi_clearance=<m>"
"[REACH] tower_dist_start=<m> tower_dist_end=<m> reached=<bool> prompt_shown=<bool>"

【必须自己跑，门槛】
新增 `-demo-walk-to-tower`：从台阶顶 (0, deck, −9) 出发，朝钟楼方向自动行走 10 秒：
- `reached` 必须 true：`tower_dist_end ≤ 2.6 m`
- `prompt_shown` 必须 true（进入半径时「按 E 查看」应出现）
- 沿途 `[BLOCK]` 条数 ≤ 2（即基本没有卡住）

回归：`-demo-walk-stairs`（on_deck=true）、`-demo-walk-into-church`（被门挡住 clearance ≥ 0.25）、`-demo-walk 6`（distance ≥ 15）、`capture_runtime.sh` exit 0、`measure_shot.py` magenta_px=0。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改美术材质/山体/雪面/相机机位表/POI 文案/measure_shot.py。
