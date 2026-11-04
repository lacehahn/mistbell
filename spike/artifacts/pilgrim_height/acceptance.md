# 人物身高任务验收（2026-11-04）

尺寸修正通过；**任务书要求的三项演示全绿未达成**：reveal 的现有失败在修改前后均存在，没有为取得绿色结果改低门槛或修改场景效果。

## 实现

- `SpikeScene.cs`：`_ScaleY=1.81`，`_ScaleX=1.81*64/80=1.448`。
- `PlayerController.cs`：去掉启动时强制归一到150px及室内按相机缩小材质的逻辑。保持室内外相同的物理身高。
- `[CAM]` 修正旧的机位日志，按当前实际机位 `(0,1.55,-7.2)`、FOV35测量；900p实测341.29px，因此门槛为307.161–375.419px（±10%，跨分辨率以900p归一）。任务书186px是估计，继续追求此数会破坏真实身高。没有移动玩法相机或修改FOV。
- `PilgrimAnimator.cs`：使用图集α≥128的实际包围盒量可见身高，覆盖全部24帧。
- `PilgrimHeightBuild.Run`：仅更新当前场景的人物材质并出macOS包，不重建场景；保留已有美术修改。
- 进堂演示在原有断言完成后，新增椅旁同场景截图，不影响玩法落点。

## 命令与实测

```
Unity -batchmode -quit -projectPath spike/project -executeMethod PilgrimHeightBuild.Run
bash spike/tools/accept_pilgrim_height.sh --player-only
```

首条命令实际使用本机完整Unity路径并写入 `spike/logs/pilgrim_height_build.log`；构建Succeeded，0 errors。验收脚本最终exit=1，正确拒绝reveal假绿。

| 验收项 | 数字／结果 |
|---|---|
| 可见身高（全部帧） | 1.69688–1.71950m，均落在1.70±0.03m |
| `[PILGRIM_SIZE]` | world_h=1.71950 aspect_preserved=true collider=1.7；outdoor/indoor/bench均通过 |
| 材质宽高比 | 1.448/1.81=0.8，与64/80一致 |
| 取景 | 341.29px@1600×900；±10%门槛通过 |
| 门洞 | 在入口z=-9..-7、中心x=-2±0.3的0.1m网格，由地板+1.7向上射线测阻挡碰撞体；最小头顶余量1.86461m；实际走入通过，无穿模 |
| 椅背与排距 | 当前实测椅背0.91052m、排距0.80230m、净空0.28m，双侧6排可见；任务书1.1m排距已不是当前场景值 |
| 祭坛 | 当前`Interior_Altar`渲染包围盒高度1.00000m，任务书1.05m为近似参考；本次未改家具 |
| 跟随高度 | LookHeight保持1.60m，位于人物上部、低于头顶，目视合理 |
| `-demo-enter-church` | INT_PASS所有布尔项True，obstacles=True、walked=True；dy=0、FOV50、lamps20、shafts3 |
| `-demo-walk 6` | distance=15.395m、ground_error_max=0.000000、raycast_misses=0、fps=59.83、fps_ok=true |
| `-demo-reveal` | FAIL：ratio20_80=1.431238<1.6；bell_once=True，pulse_restored=True |

长椅检查仅确认现有家具的几何净空与站立人物比例；本项目未实现坐姿，本次没有新增坐姿系统。

## reveal 对照与未完成项

临时使用修改前原始 `PlayerController.cs`（与当前基线提交8c004dc相同）以及1.45材质构建并独立执行 `-demo-reveal`；随后恢复最终代码和1.81材质，重新构建并执行完整三项验收。最终工作区、播放器均为新版本。

- 修改前：`[REVEAL] gate=FAIL ratio20_80=1.429020 bell_once=True pulse_restored=True`。
- 修改后：`[REVEAL] gate=FAIL ratio20_80=1.431238 bell_once=True pulse_restored=True`。
- 两版`CLOSEUP`均打印stone_tile_m=1.0，而现有断言仍要求1.8，因此closeupPass也不成立。
- `run_demos.sh`仅扫描小写failed/ERROR/Exception，漏掉`gate=FAIL`，会错误汇总rc=0 bad=0；本任务新增验收脚本直接检查正向通过标记并返回失败。未修改通用脚本、reveal代码、石材、雾或阈值。

原始日志：`spike/logs/pilgrim_height_{build,interior,walk,reveal}.log`，对照日志 `spike/logs/pilgrim_height_baseline_reveal.log`。机器可读数字及截图SHA：[measurements.json](measurements.json)。

## 截图

- [interior_inside.png](../../project/Shots/interior_inside.png)：原进堂机位，人物头顶明显高于靠背。
- [pilgrim_scale.png](../../project/Shots/pilgrim_scale.png)：同场景人物站在椅旁，0.91m椅背与祭坛同时可见，比例正常；使用临时检视机位，没有调整家具或玩法相机。

两张都是最终构建后新图，均1600×900，已目视检查。未修改AGENTS.md，未提交。
