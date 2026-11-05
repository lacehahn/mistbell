# 交接与断点（随时可暂停 / 5 天后继续）

> 更新时间：2026-11-05 12:30
> 用途：任何时候停下来，都从这份文件的「下一步」接着做。每个完成项都有 commit + tag。

## 当前状态
- 全流程验收：**P0=0 / P1=0 / P2=0**
- 2D 攀爬：51.0 s 全程 → 0.63 s 白雾转场 → 3D `TerraceLanding`，相机 11.500 m
- 3D：走 6 秒 17.747 m、进堂 `[INT_PASS]` 全 True（`lamps=20`）、reveal `gate=pass`
- 素材：11 张 2D 攀爬贴图已入库并接入（`spike/art-prompts/staged/side_*.jpg`）

## 回滚点（按时间顺序）
| tag | 含义 |
|---|---|
| `checkpoint-climb-before` | 3D 台阶改造前 |
| `checkpoint-climb-v1` | 3D 3 段台阶 + 抬头相机 + 海拔雾 |
| `checkpoint-climb-v2` | 台阶贴图去暖调 |
| `checkpoint-slope` | 3D 地形随路抬升 |
| `checkpoint-climb2d-art` | 2D 攀爬接入真素材 |
| `checkpoint-p1-clear` | 全流程 P1 清零（当前） |

构建产物备份：`spike/project/Builds/backup/Mistbell_preclimb.app`
回滚命令：`git reset --hard <tag>`（构建产物需重新出包）

## 已知取舍（不是 bug）
- 3D 路面贴图仍是 **2.4 m** 一铺（用户判定"不致命"，未改）
- 3D 路面在 2D 攀爬段存在感很弱：现在攀爬全走 2D，3D 路面只在教堂前一小段出现
- 2D 攀爬仍是"雪坡上的小路"，尚未做出悬崖与纵深
- 用户额度：5 天 8%、5 小时 50% —— **每轮只派一个小任务，跑完立刻 commit + tag + 更新本文件**

## 下一步（按顺序做，一次一项）

### A. 2D 攀爬：悬崖与纵深（未开始）
1. **下坡侧悬崖**：路带下方 120–200 px 做出"断下去"的悬崖面（雪檐 + 岩壁 + 谷雾），
   让"一侧是山、一侧是崖"成立
2. **纵深雾层**：上坡面与路带之间加 8–12% 的冷雾，压出空气感
3. **前景剪影**：底部 60–90 px 加一层近处雪檐剪影（视差 1.15），压住画面
4. 判据：`[CLIFF] drop_h=120–200 fog=8–12% fg_silhouette=true`

### B. 3D 教堂：细活（未开始）
1. **墙面砌块**：从 3 档加到 4 档明度，灰缝不均匀（宽度 ±40%）
2. **屋顶**：瓦片排列加 2% 随机错位，檐口加深 1 层投影
3. **窗**：窗棂加投影、玻璃加一点内部梯形亮斑（模拟室内亮）
4. 判据：`[CHURCH3D] block_levels=4 joint_var=0.4 window_glow=true`

## 关键命令
```
bash spike/tools/run_demos.sh "-demo-walk 6" "-demo-enter-church" "-demo-reveal" "-demo-trail 10"
bash spike/tools/accept_climb2d.sh
bash spike/tools/rebuild_all.sh          # 全量重建（22 步，约 20 分钟）
```
（注意：`rebuild_all` 用户已免除"必须全绿"，但重建后必须复跑演示确认没吞功能）
