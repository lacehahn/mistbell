# 交接与断点（随时可暂停 / 5 天后继续）

> 更新时间：2026-11-05 12:38
> 用途：任何时候停下来，都从这份文件的「下一步」接着做。每个完成项都有 commit + tag。

## 当前状态
- 上一轮全流程验收：**P0=0 / P1=0 / P2=0**；本轮只复跑悬崖任务和既有 2D/3D 回归，不运行 rebuild_all
- 2D 攀爬：50.738 s 全程（台阶 7.000 s）→ 0.651 s 白雾转场 → 3D `TerraceLanding`，相机 11.500 m、`bad=0`
- 悬崖任务已完成：落差 160 px、雪檐 40 px、外探 16 px、阴影 12 px、谷雾向下渐隐 60 px；1471 个路边样本、`curve_errors=0`
- 纵深：冷雾 `#BCC9D5`、alpha 0.102（8-bit 量化）、高 100 px；前景 `#D5E0EA`、实测高 60–84 px、视差 1.15
- 本轮 3D 回归：走 6 秒 17.748 m、台阶/相机 `bad=0`；上一轮进堂 `[INT_PASS]` 全 True（`lamps=20`）、reveal `gate=pass`
- 素材：11 张 2D 攀爬贴图已入库并接入（`spike/art-prompts/staged/side_*.jpg`）

## 回滚点（按时间顺序）
| tag | 含义 |
|---|---|
| `checkpoint-climb-before` | 3D 台阶改造前 |
| `checkpoint-climb-v1` | 3D 3 段台阶 + 抬头相机 + 海拔雾 |
| `checkpoint-climb-v2` | 台阶贴图去暖调 |
| `checkpoint-slope` | 3D 地形随路抬升 |
| `checkpoint-climb2d-art` | 2D 攀爬接入真素材 |
| `checkpoint-p1-clear` | 全流程 P1 清零 |
| `checkpoint-cliff` | 2D 下坡悬崖 + 纵深雾 + 前景剪影（当前） |

构建产物备份：`spike/project/Builds/backup/Mistbell_preclimb.app`
回滚命令：`git reset --hard <tag>`（构建产物需重新出包）

## 已知取舍（不是 bug）
- 3D 路面贴图仍是 **2.4 m** 一铺（用户判定"不致命"，未改）
- 3D 路面在 2D 攀爬段存在感很弱：现在攀爬全走 2D，3D 路面只在教堂前一小段出现
- 竖直台阶中段与转场前，弯路已在镜头下方，悬崖/路边冷雾可见像素为 0；此处保留前景剪影，沿用原有台阶和镜头
- 用户额度：5 天 8%、5 小时 50% —— **每轮只派一个小任务，跑完立刻 commit + tag + 更新本文件**

## 下一步（按顺序做，一次一项）

### B. 3D 教堂：细活（未开始）
1. **墙面砌块**：从 3 档加到 4 档明度，灰缝不均匀（宽度 ±40%）
2. **屋顶**：瓦片排列加 2% 随机错位，檐口加深 1 层投影
3. **窗**：窗棂加投影、玻璃加一点内部梯形亮斑（模拟室内亮）
4. 判据：`[CHURCH3D] block_levels=4 joint_var=0.4 window_glow=true`

## 关键命令
```
bash spike/tools/run_demos.sh "-demo-walk 6" "-demo-enter-church" "-demo-reveal" "-demo-trail 10"
bash spike/tools/accept_climb2d.sh
python3 spike/tools/verify_climb2d_cliff.py
bash spike/tools/rebuild_all.sh          # 全量重建（22 步，约 20 分钟）
```
（注意：`rebuild_all` 用户已免除"必须全绿"，但重建后必须复跑演示确认没吞功能）

## 本轮验收证据（任务 A）
- 命令：先 `bash spike/tools/accept_climb2d.sh`，再 `python3 spike/tools/verify_climb2d_cliff.py`。
- 数字报告：[cliff_acceptance.json](../spike/artifacts/climb2d/cliff_acceptance.json)。日志位于 `spike/logs/climb2d_{build,run,walk,stairs,camera}.log`（不入库，可由命令重现）。
- 截图均为 1920×1080，已逐张检查：[坡道](../spike/artifacts/climb2d/climb2d_ramp_mid.png)、[转弯](../spike/artifacts/climb2d/climb2d_turn.png)、[台阶](../spike/artifacts/climb2d/climb2d_stairs_mid.png)。
- 只改 `Climb2D.cs` 的 2D 软件合成；3D 场景、相机、教堂、室内无修改。下一项为上面的 B，本轮未开始。
