# 祭坛第三轮：原机位遮挡审计（2026-11-04）

**未通过。任务允许的调整范围与“原生射线必须命中十字”同时成立时无解。** 没有修改祭坛模型位置/尺寸、图标墙结构/碰撞、原游戏相机或室外光照；没有把无效候选保存进场景。新增了可复算诊断、原机位运行时日志和 PNG 复核脚本，不将诊断完成称为修复完成。未修改 AGENTS.md，未提交。

## 本轮实际改动

- `AltarCrossVisibility.cs`：先对全部现有物理层执行 `Physics.RaycastAll`，不忽略 `Interior_Iconostasis`。另将实际祭坛及壁龛/拱框网格复制成临时 MeshCollider，逐一 `Collider.Raycast`，记录视觉遮挡；临时查询对象立即停用后销毁，不写场景、不替换原碰撞体、不作为成功代理。
- `AltarBuild.Verify` 使用上述诊断，替代 v2 只检查壁龛、不检查原墙碰撞体的判断。只有命中祭坛实际高处网格才可能输出 `visible_from_cam=true`，本轮所有结果均为 false。
- `DemoInterior` 在原有 `interior_inside.png` 截图位置调用诊断，使用当帧真实相机和分辨率。相机位置、行进路线、演示断言、光照均保持原值。
- `AltarCrossAudit.Run`：在内存中检查候选，结束不保存场景。`verify_altar_v3.py`：读取实际相机投影 ROI、原始截图和 benchmark，输出 [测量 JSON](altar_v3_measurements.json)，失败时明确 exit=1。

## 阻挡证据

全部坐标为 Unity 世界坐标，地板 `FloorY=0.3740 m`。

| 对象 / 参数 | 实测 |
|---|---|
| 当前祭坛根 z | 8.7200 m |
| 当前十字顶端内部点 y / z | 2.5060 / 9.0100 m |
| 原截图真实相机 x / y / z | -2.0000 / 2.1240 / -4.7748 m |
| `Interior_Iconostasis` 原 MeshCollider bounds | min=(-3.6000,0.3740,7.4250)，max=(-0.4000,2.8490,7.5750) |
| 原生射线首个命中 | `Interior_Iconostasis`，y=2.4621，z=7.4250 |
| 视觉网格射线首个命中 | `Interior_V2NicheBack_1`，y=2.4672，z=7.6100 |
| 中央壁龛当前视觉下沿 | y=1.6940 m，地板上方 1.3200 m |

`wall_bottom_y` 明确定义为中央壁龛视觉网格下沿，原墙碰撞体的完整 bounds 另见 `[ALTAR_WALL_COLLIDER]`。这两层必须分开看：隐藏的旧墙 Renderer 并不代表碰撞体消失；单纯修视觉也不能通过任务第 2 条。

运行时原始日志 [altar_model3_interior.log](../../logs/altar_model3_interior.log)：

```text
[ALTAR_CROSS] altar_z=8.7200 cross_tip_y=2.5060 wall_bottom_y=1.6940 visible_from_cam=false hit=Interior_Iconostasis hit_y=2.4621 hit_z=7.4250 native_hit=Interior_Iconostasis visual_hit=Interior_V2NicheBack_1 visual_hit_y=2.4672 visual_hit_z=7.6100 camera=(-2.0000,2.1240,-4.7748) cross_height=1.10 visual_lower_raise=0.00 tip_screen=(960.00, 516.45, 13.79) image=1920x1080
```

## 允许范围的组合验证

编辑器使用 v2 已记录的原机位 (-2,2.124,-4.78)，与本轮真实机位 z 只差 0.0052 m，且真实机位另外独立验证失败。[完整审计日志](../../logs/altar_model3_audit.log) 含 17 条射线：1 条基线、12 条本轮允许范围候选、4 条旧基准歧义边界验证，**可见数为 0**。

| 祭坛根 z | 从当前 8.72 移动 | 十字高 | 下沿额外抬高 | 结果 |
|---|---|---|---|---|
| 8.60 / 8.56 / 8.52 | -0.12 / -0.16 / -0.20 m | 各测 1.10 / 0.95 m | 各测 0 / 0.08 m | 12/12 命中原图标墙碰撞，视觉也被中央壁龛挡住 |
| 8.40 | -0.32 m，仅诊断 | 各测 1.10 / 0.95 m | 各测 0 / 0.08 m | 4/4 失败；覆盖“从 v1 的 8.60 再前移 0.20”的宽松解释 |

诊断对十字高度变体复制并缩放十字所在狭窄空间内的实际网格顶点，下沿变体只抬底边顶点；原资产无修改。横杆从 0.34 缩至 0.30 m 不改变向竖杆顶部中心的射线，也不会移除前方整块中央背板，因此无法改变这些失败结果。

这也不是采样间隙：对于允许区间，十字顶部 y∈[2.356,2.506]、z∈[8.81,8.89]。射线穿过墙前表面 z=7.425 时，y 始终约为 2.331–2.467，x=-2，落在原碰撞体中央实体面内。高度降低反而让射线更低。视觉背板下沿最多只能达到 1.774，而射线经过背板时仍约为 2.334–2.473；最低高度方案也需要约 0.64 m 的下沿抬升才可能露出顶点，超过允许的 0.08 m，且依然不能解决原墙碰撞命中。

## 截图与像素复核

[本轮原始 interior_inside.png](../../project/Shots/interior_inside.png)，1920×1080，SHA-256：`f0e73ba3b024fbc443969379edeb4ccbbe0e76efb39957f47da236d99f497084`。已目视检查，十字顶端不可辨认。

ROI 由实际相机投影十字竖杆上段的两个世界点得到，横向 ±0.055 m，纵向为顶端内部点以下 0.22–0.03 m，z 为竖杆近表面。原始投影为左下原点 `(955.369,497.983)–(964.628,513.982)`；取内部整数像素后，PNG 左上原点矩形 `[956,567,964,582)`，宽 **8 px**、高 15 px。背景为同高、紧邻左右的两个等宽条带，取均值。sRGB 解码后按线性 Rec.709 求亮度：

| 指标 | 实测 | 结果 |
|---|---:|---|
| 竖杆预期投影区域亮度 | 0.00361876 | 这里实际看到的是遮挡壁龛 |
| 两侧背景均值 | 0.00423041 | 同图、同高、等宽 |
| 相对背景增亮 | **-14.46%** | 未达到 +15% |
| 宽度 | 8 px | 宽度符合，但不能证明十字可见 |

没有把背景中的其他亮线当十字，也没有移动 ROI、关闭墙碰撞或改相机来制造通过。

## 回归和预算

| 项目 | 本轮结果 |
|---|---|
| Unity 开发播放器构建 | Succeeded，0 errors |
| `-demo-enter-church` | 全绿；`light=True domeLight=True ceilingSeen=True domeSeen=True visible=True obstacles=True walked=True` |
| 模型三角面 / 材质 | 8880 / 2，原资产保持不变 |
| 室内 drawcalls / 平均 FPS | **236 / 232.87**，通过 ≤260 / ≥200 |
| 室内 1% low | 166.45 FPS，仅记录 |
| 任务整体验收 | **失败：射线与像素门槛均失败** |

性能环境：Metal / Apple M6、1920×1080、开发播放器、vSync=0，每机位至少 180 帧且至少 3 秒预热、300 帧采样。[性能 JSON](../../logs/altar_model3_bench.json)、[原始性能日志](../../logs/altar_model3_bench.log)、[构建日志](../../logs/altar_model3_build.log)。无模型/场景生成操作，不重新烘焙任何室外资产。

## 复现

以下命令从仓库根运行。Unity 命令串行执行；播放器使用 `open -n -a`，演示与性能采样之间等待上一个 Mistbell 退出，并保持显示器唤醒。

```sh
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project -executeMethod AltarCrossAudit.Run -logFile spike/logs/altar_model3_audit.log
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project -executeMethod PerformanceBuild.Run -logFile spike/logs/altar_model3_build.log
caffeinate -dimu -t 180
```

在另一个终端运行两次播放器；每次都等退出后继续：

```sh
open -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -demo-enter-church -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/altar_model3_interior.log"
open -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -bench -bench-foreground -bench-output "$PWD/spike/logs/altar_model3_bench.json" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/altar_model3_bench.log"
python3 spike/tools/verify_altar_v3.py
```

最后命令本轮预期 **exit=1**，并输出 `accepted=false`、`ray_pass=false`、`pixel_pass=false`、`demo_pass=true`、`budget_pass=true`。没有把复现失败当测试全绿。

## 下一步所需的范围变更

⚠️ 待决策：必须放宽至少一个约束，才能继续修复。若保持墙及碰撞完全不动，可以考虑将祭坛/十字移至墙前；仅按十字中心深度算，祭坛根需低于约 z=7.135，较当前 8.72 前移约 **1.6 m**，远超 0.20 m。该数值是几何下界估算，尚未实施或保证构图/移动验收，须另做场景位置与碰撞一致性验证。另一方向是重新设计墙的视觉开口和对应碰撞，但本轮明确禁止修改碰撞，未实施。不能只抬视觉底边后声称通过原生射线验收。
