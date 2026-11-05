# 任务 A：2D 攀爬——做出"悬崖 + 纵深"

## 现状（`climb2d_ramp_mid.png`）
路带 + 上坡面 + 树已经有了，但**下坡侧没有"断下去"的悬崖**：
整幅是连续的雪坡，路像画在斜坡上的一条带子，没有"一边是山、一边是崖"的危险感。

## 要做的（三项，都在 2D 合成层里，别动 3D）

### 1. 下坡侧悬崖（最重要）
- 路带下方 **120–200 px** 做出悬崖立面：
  - 上缘是**雪檐**（snow cornice）：30–50 px 厚，向外探出 10–20 px，底部有 8–12 px 深色阴影
  - 中段是**岩壁**：可用 `side_ground_cliff` 竖向拉伸或用 `side_slope_up` 竖切一段
  - 下部渐隐进**谷雾**（色 `#B9C7D4`，alpha 0.5→0 向下 60 px）
- 悬崖走向要跟随路带弯曲（路弯它弯）

### 2. 纵深雾
- 在**上坡面与路带之间**叠一层冷雾：alpha **8–12%**、色 `#BCC9D5`，高度 80–120 px，
  让"山在上、路在中"有空气隔开

### 3. 前景剪影
- 画面底部 60–90 px 加一层近处雪檐/岩角剪影（视差 **1.15**），
  色 `#D5E0EA`（比雪略暗、比谷雾亮），压住画面下缘

## 验收
- `[CLIFF] drop_h=120–200 cornice=30–50 fog=8–12% follows_curve=true`
- `[DEPTH] haze_alpha=0.08–0.12 fg_silhouette=true fg_parallax=1.15`
- 截图：`climb2d_ramp_mid.png`、`climb2d_turn.png`、`climb2d_stairs_mid.png`
- 走完全程仍 45–55 s、转场 `transition=ok`、进 3D `bad=0`
- 3D 演示不受影响（`-demo-walk 6` ≈17.7 m、`bad=0`）

## 硬约束
- 不改 3D 场景/相机/教堂/室内；不要求 rebuild_all 全绿；
- 完成后**必须**：commit + `git tag -f checkpoint-cliff` + 更新 `docs/12-handoff.md` 的下一步。
