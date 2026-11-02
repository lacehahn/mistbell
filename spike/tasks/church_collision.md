只做一件事：给教堂与挡土墙加**碰撞**，让玩家不能穿过建筑、不能站到墙顶。只动碰撞与测试。

必读：`Assets/Scripts/PlayerController.cs`、`SpikeScene.cs` 的 `ImportChurch()` 与台地/挡土墙生成部分。

【BUG（我实测复现）】
`-demo-walk-stairs`：`walk_start=(0,-1.677,-16)` → `walk_end=(0,1.927,5.648)`，deck_y=0.286。
玩家从石阶上来后**直接穿进教堂内部**，最后停在教堂基座高度 +1.93。
原因：教堂网格（Stone / Roof_Slate 两个 submesh）**没有碰撞体**；挡土墙也没有。

【要改】
1) **教堂碰撞**：导入 FBX 后给教堂根节点加 `MeshCollider`（convex=false，静态），或用两个 BoxCollider 近似（主堂 18×12.5×9 + 塔/钟楼各一个），只要保证玩家无法进入建筑内部。要求玩家与墙面最小间距 ≥ 0.25 m 时被挡住。
2) **挡土墙碰撞**：28 段挡土墙每段加 BoxCollider；墙顶（deck+1.62）**不可站立**——即玩家从台阶上来只能上台地面，不能跳到墙沿。
3) **台阶**：保留 step-up 0.28 m 与隐形坡；台阶碰撞保持原样。

【自检打印】
"[COLLIDE] church_colliders=<n> wall_colliders=<n> player_radius=<m> blocked_at_wall=true"
"[MOVE] stair_test_start=(x,y,z) end=(x,y,z) deck_y=<f> on_deck=<bool> inside_church=false"

【必须自己跑，且门槛收紧】
1) `-demo-walk-stairs` 改为**只走 4.5 秒**（够上台阶、不够穿过广场）：
   - `on_deck` 必须 true：`|end.y − deck_y| ≤ 0.15`
   - `inside_church` 必须 false：终点不得落在教堂包围盒内（x∈[−14,14]、z∈[−11,13]）
   - `distance ≥ 6 m`
2) 追加 `-demo-walk-into-church`：从台地面正对门直走 4 秒，必须被挡在门外：`end.z ≤ −1.2`（门外侧）且 `inside_church=false`。
3) 回归：`-demo-walk 6`（distance ≥ 15 m、贴地 ≤ 0.02）、`-demo-reveal`（三图仍在、钟声只一次）、`capture_runtime.sh` exit 0、`measure_shot.py` magenta_px=0。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改美术材质/山体/雪面/相机机位表/POI 文案/measure_shot.py。
