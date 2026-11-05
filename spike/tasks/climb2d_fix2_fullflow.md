# 任务：修观感三处 + 跑全流程找高优先级 bug

## 一、修观感（`climb2d_ramp_mid.png` 我复核后提的三条）

### 1. 暗边不要像描线
- 现在是路两侧 **4 px 均匀深色线**，右上还绕成闭合圈，像手绘等高线；
- 改成 **12–20 px 柔和渐变暗边**：靠路一侧 alpha 0.45，向外 20 px 渐到 0；
  **不要在路带闭合处画整圈**，只在"靠山那侧"和"下坡那侧"各画一条，方向跟随路带走向。

### 2. 上坡侧要"竖着压下来"
- 现在读成梯田/台地，因为上坡面素材的岩层被铺成了水平带；
- 在**画面顶部**加一道竖向岩石断面：高 **120–180 px**，宽满屏，
  可用 `side_ground_cliff` 旋转 90° 或 `side_slope_up` 竖切一段；
- 它应该**压在路的上沿之上**，形成"山体立面 → 路 → 下坡"的三段式；
- 断面底部接 30–50 px 的柔和过渡，不要硬切。

### 3. 台阶雪边压薄
- `side_step_oblique` 的雪边从当前厚度压到 **≤6 px**，48 级堆起来不能变成"雪条纹梯田"。

## 二、跑全流程，报高优先级 bug
依次跑并记录：
```
bash spike/tools/run_demos.sh "-demo-walk 6" "-demo-walk-stairs" "-demo-walk-to-tower" \
  "-demo-enter-church" "-demo-cinema" "-demo-reveal" "-demo-journal" \
  "-demo-settings" "-demo-save" "-demo-load" "-demo-trail 10"
bash spike/tools/accept_climb2d.sh
```
要求输出一张**问题清单**，按严重度排序，每条写：
`现象 / 命令 / 关键日志行 / 我判断的严重度(P0 阻断/P1 明显/P2 轻微)`

重点看这些已知风险：
- 3D 走 6 秒的距离是否仍 ≈17.7 m（换 2D 后有没有被影响）
- `-demo-enter-church` 的 `[INT_PASS]` 各项、`lamps` 数目、`obstacles`
- `-demo-reveal` 的 `gate` 与 `ratio`/`far_h_pct`
- `-demo-save`/`-demo-load` 的存档落点
- 2D→3D 转场后角色是否准确落在 `TerraceLanding`、相机是否 11.5 m
- 任何 `bad>0`、`exit!=0`、`error CS`、`NullReference`

## 验收
- 三条观感修完，附 `climb2d_ramp_mid.png`、`climb2d_start.png`、`climb2d_turn.png`；
- 完整问题清单（P0/P1/P2 分组），即使全绿也要写"未发现 P0/P1"；
- 不要求 rebuild_all 全绿；不改 3D 场景/相机/教堂/室内。
