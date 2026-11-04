# 王门视觉开口验收（2026-11-04）

通过本轮视觉门洞、原机位视觉射线、进堂演示及室内预算验收。原移动碰撞完整保留；原生 Physics 射线仍命中墙，视觉射线命中祭坛。两种射线结果均记录，不将物理射线描述为穿墙成功。未修改 AGENTS.md，未提交。

## 实现

- 仅移除中央 `Interior_V2NicheBack_1`，替换中央旧框为 `Interior_RoyalDoor_Frame`；两侧背板与框不动。中央框拱顶维持世界 y=3.2340 m，原高度/横向缩放不改；门柱延伸至地板 y=0.3740 m，形成落地门洞。净宽0.86 m / 净高2.86 m，拱半径0.43 m，金带宽0.04 m / 向会众方向出挑0.02 m，复用现有 `M_InteriorV2Gold`（#C9A227）。未添加可选门片。
- 祭坛仍在(-2,FloorY,8.72)，十字仍高1.10 m。未重新生成祭坛、修改圣像、移动灯光、修改原游戏相机或室外资产。
- `RoyalDoorBuild.Run` 在既有场景内局部更新；更新前后比较全部碰撞组件的序列化内容及变换矩阵、两侧拱与背板网格及变换，均一致。重复安装两次，门框数量仍为1。`BuildSanctuary` 集成相同安装函数，后续全场景生成可保留此修复。
- 演示对拱框的计数改为“两侧旧框 + 中央王门框”，总数仍须为3；`TestInteriorObstacles`、步行路径、照明/构图门槛不变。3 m近景在所有原演示断言结束后，用单独相机拍摄。

## 日志与射线定义

```text
[ROYAL_DOOR] center_arch=-2.0000/1.8040/7.5500 backing_removed=true door_w=0.8600 door_h=2.8600 frame_mat=M_InteriorV2Gold frame_color=#C9A227 frame_width=0.0400 projection=0.0200 collider_changed=false
[ROYAL_DOOR_INVARIANTS] colliders_identical=true side_arches_identical=true idempotent=true
[ROYAL_DOOR_RAY] mode=rendered_geometry hit=Interior_Altar_Model native_hit=Interior_Iconostasis pass=true camera=(-2.0000, 2.1240, -4.7750)
```

原机位相机为(-2.0000,2.1240,-4.7750)，目标十字内部顶点为(-2,2.5060,9.0100)。实际视觉命中点 y=2.5053 / z=8.9850，命中 `Interior_Altar_Model`。查询实际不透明室内源网格；透明光柱/地面光斑不作为实体遮挡，保留的批处理源网格只查询一次，不重复查询合批副本。临时查询碰撞体随后立即停用并销毁，不改变游戏碰撞。编辑器与运行时均通过。

`native_hit=Interior_Iconostasis` 是保留完整墙碰撞的预期结果。任务写明门洞只影响视觉与射线，本报告的通过射线指视觉网格射线；若另要求原生全层 `Physics.Raycast` 穿过该碰撞体，则与“碰撞不变”不能同时满足，本轮没有实现也没有伪称实现。中央背板已删除，旧诊断中的 `wall_bottom_y=Infinity` 表示不再存在该背板下沿，不是实际坐标。

## 截图与回归

- [interior_inside.png](../../project/Shots/interior_inside.png)：原游戏相机、原景深，中央拱后十字及祭坛可见。
- [royal_door_closeup.png](../../project/Shots/royal_door_closeup.png)：会众方向相机(-2,1.8040,4.55)，正对中央拱平面z=7.55，距离3.000 m，FOV60°。保留场景后期，未隔离模型或补光。金框落地、十字及祭坛清楚可见；两侧仍为封闭龛。两张1920×1080截图均已目视检查，SHA-256见测量JSON。
- `-demo-enter-church`：light=True / domeLight=True / ceilingSeen=True / domeSeen=True / visible=True / **obstacles=True** / walked=True。
- 开发播放器：Succeeded / 0 errors；`git diff --check`通过。

| 指标 | 实测 | 门槛 |
|---|---:|---:|
| 祭坛三角面 | 8880 | ≤9500 |
| 祭坛材质 | 2 | ≤2 |
| 室内drawcalls | 236 | ≤270 |
| 室内平均FPS | 237.07 | ≥200 |
| 室内1% low | 173.90 | 记录 |

环境：Apple M6 / Metal / Unity6000.3.27f2 / 1920×1080开发播放器；vSync=0，每机位至少180帧且3秒预热，300帧采样。预算取室内机位；室外台地278 drawcalls不属于本次室内预算。原始数据：[测量JSON](royal_door_measurements.json)、[build日志](../../logs/royal_door_build.log)、[进堂日志](../../logs/royal_door_interior.log)、[bench JSON](../../logs/royal_door_bench.json)。

## 复算

仓库根目录运行；Unity和播放器依次完成后再运行下一步。播放器保持前台和显示器唤醒。

```sh
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project -executeMethod RoyalDoorBuild.Run -logFile spike/logs/royal_door_build.log
open -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -demo-enter-church -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/royal_door_interior.log"
open -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -bench -bench-foreground -bench-output "$PWD/spike/logs/royal_door_bench.json" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/royal_door_bench.log"
python3 spike/tools/verify_royal_door.py
```

末项本次exit=0，accepted=true。场景、生成网格、构建和截图沿用仓库现有忽略规则留在本地；提交源文件后需要以上命令重新生成。
