# 任务：把用户三张阶石贴图接到 3 段台阶上

## 素材（已入库）
- `spike/art-prompts/staged/stone_step_tread.jpg`（1254²，接缝 2.2/2.2）
- `spike/art-prompts/staged/stone_step_riser.jpg`（接缝 2.5/7.1）
- `spike/art-prompts/staged/stone_step_snow.jpg`（雪盖约 50%）

## 现状
台阶（3 段 × 10 级、级高 0.16 m、踏深 0.32 m）目前**全部用 `path_stone_a` 顶替**，
踢面因此读成"深灰发亮的湿石"，这是本轮唯一明显假的地方。

## 要做的
1. **踏面**：`stone_step_tread`，世界投影 **1.2–1.5 m** 一铺；崩口边朝前缘（踏步前端），
   让那条投影边读作石板缝；`_BaseColor` 不动（不要提亮）。
2. **踢面**：`stone_step_riser`，横向铺 **1.2 m**、竖向压缩到 1 铺（踢面只有 0.16 m 高）；
   `_Smoothness ≤ 0.10`、`_Specular ≈ 0`，**必须去掉现在的湿石高光**。
3. **雪边**：`stone_step_snow` 以 **0.6–0.8 m** 小尺度叠在每级踏步的最前缘 0.06–0.10 m 宽带上，
   混合模式加法 + alpha 0.55–0.7，让台阶边缘像被雪压过。
4. 三张贴图都走 Repeat + mipmap + aniso 4，Bilinear。

## 验收（只做这几项，不要求 rebuild_all 全绿）
- `[STEP_TEX] tread=1.35 riser=1.2x1 snow=0.7`；
- `[STEP_TEX] riser_smoothness≤0.10 spec≤0.02`；
- 截图：`climb_steps.png`（正对台阶）、`play_view.png`；
- 目视：**踢面不再是深灰发亮**、踏面能看出磨损与崩口边、台阶前缘有雪压带；
- `-demo-walk 6` 仍走完（约 17.7 m）、`bad=0`；`-demo-enter-church`、`-demo-reveal` 退出码 0；
- 室外 `fps ≥ 200`。

## 硬约束
- 不动台阶几何（级数/级高/踏深）、不动围栏/教堂/远山/室内/人物/相机参数。
