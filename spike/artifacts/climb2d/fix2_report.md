# climb2d_fix2_fullflow 验收（2026-11-05）

只修改 `Assets/Scripts/Climb2D.cs` 的 2D 绘制，未改 3D 场景、机位、教堂、室内、退出判据或验收脚本；未修改 AGENTS.md。三处观感与 2D 专项验收通过；全流程发现 1 项 P1，不能据脚本 FAIL=0 宣称全绿。

## 三处观感

- 删除 4px 硬描边与原 56px 均匀阴影；仅沿靠山/下坡侧做 20px 外向渐隐（alpha 0.45→0），跳过陡直闭合端，未绘制整圈轮廓。
- 顶部全屏竖向岩壁：旋转采样现有 side_ground_cliff，实心高 160px，底部额外 40px SmoothStep 过渡；路面保持前景遮挡关系。
- side_step_oblique 仅在 2D 运行时重采样，雪边最多 1 逻辑像素，即 4px≤6px；48 级仍保留，未修改源美术。
- 新截图均 1920×1080，已目视复核：[ramp_mid](climb2d_ramp_mid.png)、[start](climb2d_start.png)、[turn](climb2d_turn.png)。补充 [stairs_mid](climb2d_stairs_mid.png)。
- 独立用截图和采样掩码重算 CIE L*：start/ramp_mid/turn 路雪差 17.023 / 17.278 / 17.285，均≥12，和运行时日志差<0.02。

## 命令及结果

按任务书顺序执行：

```sh
bash spike/tools/run_demos.sh "-demo-walk 6" "-demo-walk-stairs" "-demo-walk-to-tower" \
  "-demo-enter-church" "-demo-cinema" "-demo-reveal" "-demo-journal" \
  "-demo-settings" "-demo-save" "-demo-load" "-demo-trail 10"
bash spike/tools/accept_climb2d.sh
```

全流程汇总脚本 exit=0 / FAIL=0（原始[汇总](../../logs/climb2d_fix2_fullflow.log)）；其中 rc 是超时/存活状态，不是真实播放器退出码。2D 专项最终 exit=0（[日志](../../logs/climb2d_fix2_acceptance.log)）。第一轮截图岩壁纹理偏横向，调整纯 2D 采样比例后，第二条命令完整重跑通过。11 项 3D 流程使用此前构建；两构建仅存在 2D 绘制差异，后一次含 3D walk/stairs/camera 回归。

| 演示命令 | 汇总 rc / bad | 本次原始日志 |
|---|---|---|
| `-demo-walk 6` | 0 / 0 | [run_121035_-demo-walk6.log](../../logs/run_121035_-demo-walk6.log) |
| `-demo-walk-stairs` | 0 / 0 | [run_121045_-demo-walk-stairs.log](../../logs/run_121045_-demo-walk-stairs.log) |
| `-demo-walk-to-tower` | 0 / 0 | [run_121052_-demo-walk-to-tower.log](../../logs/run_121052_-demo-walk-to-tower.log) |
| `-demo-enter-church` | 0 / 0 | [run_121104_-demo-enter-church.log](../../logs/run_121104_-demo-enter-church.log) |
| `-demo-cinema` | 0 / 0 | [run_121126_-demo-cinema.log](../../logs/run_121126_-demo-cinema.log) |
| `-demo-reveal` | 0 / 0 | [run_121141_-demo-reveal.log](../../logs/run_121141_-demo-reveal.log) |
| `-demo-journal` | 0 / 0 | [run_121157_-demo-journal.log](../../logs/run_121157_-demo-journal.log) |
| `-demo-settings` | 0 / 0 | [run_121212_-demo-settings.log](../../logs/run_121212_-demo-settings.log) |
| `-demo-save` | 0 / 0 | [run_121228_-demo-save.log](../../logs/run_121228_-demo-save.log) |
| `-demo-load` | 0 / 0 | [run_121243_-demo-load.log](../../logs/run_121243_-demo-load.log) |
| `-demo-trail 10` | 0 / 0 | [run_121246_-demo-trail10.log](../../logs/run_121246_-demo-trail10.log) |

## 数字与关键风险复核

- 构建 Succeeded / errors=0；本次日志扫描未见 error CS、NullReference、Exception、bad>0 或 gate=FAIL。
- 3D walk6：17.748m，贴地误差 0m，漏射线 0，bad=0；最后专项重跑 17.747m，bad=0。
- 3D stairs：6.490m，on_deck=true，actual_church_overlap=false，bad=0；钟楼接近 11.994→1.072m，reached=true / prompt_shown=true / blocks=0。
- `[INT_PASS] light=True domeLight=True ceilingSeen=True domeSeen=True fx=0.785 visible=True obstacles=True walked=True z=0.43 dy=0.000 fov=50.00 lamps=20 shafts=3`；colliders=66，closed_blocks=True，raycast_misses=0。
- cinema：entries=1，restore_error_m=0，input_frozen/input_restored=True，resume_distance=0.1207m，once=True。
- reveal：gate=pass，ratio=1.284931（仅诊断），far_h_pct=0.227751，far_contrast=0.055649，far_dist=115m，near_h_pct=0.646993，bell_once/pulse_restored=True；未改判据。
- journal：total=8 / visited=1 / unread=7；settings 的 cn/en/both 与 volume=0.25/0/0.8 顺序运行完成。
- save：wrote=true / SAVE_DEMO pass=true / pois=1。load：pos=(-2.0000,0.3740,5.0252)，inside=true / door=true / pois=1 / delta_m=0.000000。
- 2D：walk=40.600s，stairs=7.000s，total=50.600s，fade=0.619s；截图5张，support_error_px=0.000031，bitmap_misses=0，bad=0。
- 2D→3D：deck_support=TerraceLanding，ground_error_m=0，position=(0.000,0.286,-9.965)，camera_z_distance=11.500m，valid=True，transition=ok。
- 3D 相机回归两阶段 bad=0：最小离地间隔 3.89788 / 3.60946m。
- ArtSpike.unity、PlayerController.cs、ChurchInterior.cs、MistReveal.cs、PlayerClimb2DArrival.cs 的专项前后 SHA-256 全相同，见 fix2_protected_before/after.json；git diff 中源代码仅 Climb2D.cs。

## 问题清单（严重度排序）

### P0 阻断

未发现 P0。

### P1 明显

| 现象 | 命令 | 关键日志行与数字 | 判断 |
|---|---|---|---|
| 足迹演示实际失败，但全流程脚本漏报为通过 | `bash spike/tools/run_demos.sh "-demo-trail 10"`（已在完整11项中执行） | `[TRAIL] spawns=0 paving_skipped=39 static_kept=64 distance=29.61`；`[TRAIL_SHOT] spawns=0 walk_pass=true`；汇总却为 `rc=0 bad=0` / `FAIL=0` | **P1：验收假绿**。PlayerController.cs:941 要求 spawns≥8；路段踩在铺装上而被跳过，旧演示不再覆盖雪面落脚印。run_demos.sh:31 把所有正常消失的进程设为 RC=0，错误模式也没覆盖此终止判据。按任务书只报告，未改3D或脚本。 |

为排除仅凭源码推断，最终构建额外重跑一次 `-demo-trail 10`，继续使用 `open -n -a` 前台启动，并通过 macOS kqueue `EVFILT_PROC / NOTE_EXIT / NOTE_EXITSTATUS` 监听该进程：**raw_wait_status=256，真实 exit=1**；该额外采集命令本身 exit=0。同次 `[TRAIL_SHOT] spawns=0 walk_pass=true`。见 [进程退出证据](fix2_trail_exit.json) 与 [复核日志](../../logs/climb2d_fix2_trail_exit.log)。这证实的是足迹演示门槛失败与汇总漏报；不据此断言雪面脚印功能损坏。

### P2 轻微

未发现独立 P2 问题。

完整机器数据：[fix2_measurements.json](fix2_measurements.json)。没有运行 rebuild_all 或修改本任务之外的3D内容。
