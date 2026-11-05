# 交接与断点（随时可暂停 / 5 天后继续）

> 更新时间：2026-11-05 13:43
> 用途：任何时候停下来，都从这份文件的「下一步」接着做。每个完成项都有 commit + tag。

## 当前状态
- 本轮仅完成 `spike/tasks/climb2d_path_read.md`：路带压暗与石板细节、半柔暗边、三处岩壁咬合；局部构建与任务验收通过，未运行 rebuild_all。上一轮全流程验收 **P0=0 / P1=0 / P2=0** 为历史记录。
- 教堂 B：4 档砌块、灰缝 ±40% / 暗 19.664%–20.072%、6–9 块一条石；屋顶 2% 错位 / 每 5–7 行阴影；7 扇窗完成窗棂投影与亮斑。详见 [验收记录](../spike/artifacts/church3d/README.md)。
- 2D 攀爬：50.776 s 全程（台阶 7.000 s）→ 0.662 s 白雾转场 → 3D `TerraceLanding`，相机 11.500 m、`bad=0`；`-demo-walk 6` = 17.747 m、`bad=0`
- 历史悬崖任务（沿路悬崖，继续保留）：落差 160 px、雪檐 40 px、外探 16 px、阴影 12 px、谷雾向下渐隐 60 px；1471 个路边样本、`curve_errors=0`
- 纵深：冷雾 `#BCC9D5`、alpha 0.102（8-bit 量化）、高 100 px；前景 `#D5E0EA`、轮廓高 60–84 px、上缘新增 20 px alpha 融合、视差 1.15
- 上轮 B 的 3D 回归（历史记录）：走 6 秒 17.747 m、进堂 / 揭示均 `bad=0`；`[INT_PASS]` 全 True（`lamps=20`）、reveal `gate=pass`；室外 FPS / drawcall = 213.04 / 242 / 223.68 / 277
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
| `checkpoint-cliff` | 2D 下坡悬崖 + 纵深雾 + 前景剪影 |
| `checkpoint-church3d` | 3D 教堂砌块、屋顶与窗户细化 |
| `checkpoint-climb2d-grade` | 2D 冷色校正、顶部雾、三处岩壁、树变体与层间融合 |
| `checkpoint-path-read` | 路带明度、12 px 半柔暗边、三处岩壁压沿 8 px 与 3 px 接触阴影（当前） |

构建产物备份：`spike/project/Builds/backup/Mistbell_preclimb.app`
回滚命令：`git reset --hard <tag>`（构建产物需重新出包）

## 已知取舍（不是 bug）
- 3D 路面贴图仍是 **2.4 m** 一铺（用户判定"不致命"，未改）
- 3D 路面在 2D 攀爬段存在感很弱：现在攀爬全走 2D，3D 路面只在教堂前一小段出现
- 竖直台阶中段与转场前，弯路已在镜头下方，悬崖/路边冷雾可见像素为 0；此处保留前景剪影，沿用原有台阶和镜头
- 用户额度：5 天 8%、5 小时 50% —— **每轮只派一个小任务，跑完立刻 commit + tag + 更新本文件**

## 下一步（按顺序做，一次一项）

### B. 3D 教堂：细活（已完成）
- [x] 墙面：4 档明度，保持 Lab 的 a*/b* 目标值（8-bit 量化最大 b* 偏差 0.1804）；灰缝宽度 3/5/7 px（基准 5 px），条石长度 1.4–1.8 倍、间隔 6–9 块。
- [x] 屋顶：2% 亚像素行距错位，每 5–7 行暗 12% 阴影；屋脊 / 山墙边缘 alpha 0.35、8 px 等效阴影。
- [x] 窗：每扇 1 竖 / 2 横，0.15 m 投影，梯形亮斑自发光 ×1.20，洞壁乘色 ×0.92。
- [x] 两张运行时截图、室外性能与三项演示验收，记录见上面的验收链接。
- [x] 相机、室内、路面、围栏、碰撞体与既有脚本未变；原始共用图集未变。场景保护摘要前后一致。

### 2D 配色与层次（已完成）
- [x] 雪冷色、石面 b*=5–7 与 L* 对比曲线 ×1.12；量化后逐张截图重算，见下表。
- [x] 移除原顶部整幅岩壁覆盖，裁取 `side_slope_up` 不透明内段；顶边 40 px 雾化至 `#DCE6EF`，三张截图顶边异常像素均为 0。
- [x] `side_ground_cliff` 竖切 3 处：高 120 / 160 / 200 px，宽 200 / 300 / 400 px，24 px 雪檐、40 px 下缘渐隐；另有 20 px 上缘和侧边融合。
- [x] 从树素材切出 3 棵独立树，绘制高度参数 90 / 150 / 240 px；每实例稳定随机翻转、明度 ±8%、位置 ±12 px，随镜头保持 0.6 视差。像素画布仍为 480×270、4 倍显示，半逻辑像素尺寸由相邻栅格取样。
- [x] 背景交界与前景上缘 20 px alpha 过渡；截图横向硬接缝检测为 0。检测口径：相邻逻辑行 RGB 平均跳变 ≥12 且覆盖宽度 ≥80%。
- [x] 五项日志、截图、全程与 3D 回归通过；189 个受保护文件（除 `Climb2D.cs` 外全部已跟踪 Assets，以及 `AGENTS.md`）SHA-256 与开工基线一致。

### 新的下一步
本轮 `climb2d_path_read.md` 已完成；由负责人评审坡道、转弯两张 1920×1080 截图后指定下一份单项任务书。当前不自动开展后续任务。

## 关键命令
```
bash spike/tools/run_demos.sh "-demo-walk 6" "-demo-enter-church" "-demo-reveal" "-demo-trail 10"
bash spike/tools/accept_climb2d.sh
python3 spike/tools/verify_climb2d_grade.py
python3 spike/tools/verify_climb2d_path_read.py # 本轮任务：像素 / 咬合 / 保护检查
bash spike/tools/accept_church3d.sh       # B：局部生成/构建 + 截图/回归/性能/像素验收
python3 spike/tools/verify_church3d.py     # 复核已有本轮证据
bash spike/tools/rebuild_all.sh          # 全量重建（22 步，约 20 分钟）
```
（注意：`rebuild_all` 用户已免除"必须全绿"，但重建后必须复跑演示确认没吞功能）

## 上轮验收证据（2D grade，以下数字为历史值）
- 命令：`bash spike/tools/accept_climb2d.sh`，完成后运行 `python3 spike/tools/verify_climb2d_grade.py`。构建 `Succeeded`、`errors=0`；全程、3D 步行 / 台阶 / 相机回归均通过。
- 数字报告：[grade_acceptance.json](../spike/artifacts/climb2d/grade_acceptance.json)；五项运行日志与独立像素验收：[grade_acceptance.log](../spike/artifacts/climb2d/grade_acceptance.log)；保护基线：[grade_protected.json](../spike/artifacts/climb2d/grade_protected.json)。
- 对比值按最终截图石面 mask 与校正前 buffer 的 L* 线性回归计算，目标 +12%，允许 8-bit 量化带来 ±0.5 个百分点。雪额外提亮后按独立 mask 检查 b*。

| 截图（均为 1920×1080） | 雪 b* | 石 b* | 对比增加 | 硬接缝 |
|---|---:|---:|---:|---:|
| [坡道](../spike/artifacts/climb2d/climb2d_ramp_mid.png) | -0.984 | 6.016 | 12.114% | 0 |
| [转弯](../spike/artifacts/climb2d/climb2d_turn.png) | -0.977 | 6.003 | 12.125% | 0 |
| [台阶](../spike/artifacts/climb2d/climb2d_stairs_mid.png) | -0.993 | 6.022 | 11.990% | 0 |

- 全程 **50.582 s**、步行 **40.600 s**、台阶 **7.000 s**、转场 **0.634 s**，`transition=ok`；进入 3D `valid=True bad=0`；6 秒步行 **17.746 m**、`bad=0`。
- 只修改 `Climb2D.cs` 的 2D 软件合成与本任务验收脚本；3D 场景、相机、教堂、室内与共享素材未变。开工时工作区干净。
- 历史 A 的 `cliff_acceptance.json` 保留作历史记录；本轮前景新增 20 px 融合，旧 `verify_climb2d_cliff.py` 对纯色前景高度的断言已由本轮像素融合验收替代。


## 本轮验收证据（path read）
- 任务范围：只改 `Climb2D.cs` 的 2D 路带与三处岩壁接触合成，以及本任务验收脚本和证据。本轮开工工作区干净。
- 已运行 `bash spike/tools/accept_climb2d.sh` 与 `python3 spike/tools/verify_climb2d_path_read.py`，均通过。Unity `Succeeded errors=0`；后者同时调用现有流程、色校正、树与接缝回归验收。
- 数字报告：[acceptance.json](../spike/artifacts/path_read/acceptance.json)；完整验收输出：[acceptance.log](../spike/artifacts/path_read/acceptance.log)；开工提交：[baseline.json](../spike/artifacts/path_read/baseline.json)；文件保护：[protected.json](../spike/artifacts/path_read/protected.json)。

| 截图（1920×1080） | 路面 L* | 邻近雪地 L* | 差值 L* | 雪 b* | 石 b* |
|---|---:|---:|---:|---:|---:|
| [坡道](../spike/artifacts/climb2d/climb2d_ramp_mid.png) | 56.535 | 95.870 | 39.334 | -0.986 | 6.070 |
| [转弯](../spike/artifacts/climb2d/climb2d_turn.png) | 57.182 | 95.818 | 38.636 | -0.980 | 6.058 |

- 路带：顶面素材 `side_ground_top_oblique` 源行 60–239，细节混合权重 **0.35**，混合结果 RGB 乘色 **0.90**。独立截图 mask 重算路面与邻近雪地 L*，两张均 ≥10；不把 RGB 的 10% 当作 L* 差 10。
- 暗边：**12 px**，alpha **0.42 → 0.27 → 0.12**（8-bit 实测 107 / 69 / 31），仅靠山 / 下坡两侧，端头不闭合；保留端头陡边过滤。
- 咬合：复用原三处岩壁切片，沿路线点 **4 / 7 / 10** 的下沿逐列贴合，分别 **50 / 75 / 100** 列；上缘遮住路带 **8 px**。岩壁素材、树素材和树绘制逻辑未改。
- 接触阴影：alpha **0.30**、真实显示高度 **3 px**。因原生画布 480×270、显示 4 倍，接触条单独在显示层绘制，仍调用原色校正函数；其部分覆盖的逻辑格从常规 grade / 路面统计中排除。独立截图验证 3 行着色、第 4 行保留底色：坡道 **78** 个样本、转弯 **175** 个样本，**像素误差 0**。
- 目视复核：坡道与回头弯均能看清雪坡上连续上升的石路；边缘为暗带、端头未描线，三处岩壁随路移动并压住路沿。
- 全程 **50.776 s**，步行 **40.600 s**、台阶 **7.000 s**、转场 **0.662 s**，`transition=ok`；进入 3D `valid=True bad=0`。`-demo-walk 6` **17.747 m**、**360** 个支持样本、`bad=0`；3D 台阶与相机回归通过。
- 色校正 / 树 / 原有悬崖 / 台阶生成方法与开工提交逐方法一致；其余 **189** 个受保护文件 SHA-256 一致，包含全部已跟踪 Assets（除 `Climb2D.cs`）和 `AGENTS.md`。3D 未改。两张截图硬接缝 **0**。
- 收尾标签：`checkpoint-path-read`。只完成此任务，不自动开始下一项。
