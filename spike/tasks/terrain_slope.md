# 任务：让"地面"跟着爬升，而不是只有路和台阶抬高

## 用户观察（我核实过代码，他说得对）
`SpikeScene.TerrainHeight()` 第 1327–1333 行：
```csharp
if(z >= -40f) return original;                       // 教堂那边保持原高度
float dx = Mathf.Abs(x - ClimbRoute.CenterX(z));     // 到路心的横向距离
float bank = ClimbRoute.Depth * SmoothStep(0,1,InverseLerp(2.5f, 4.5f, dx));
float route = ClimbRoute.Height(z) + bank + shoulder + ...;
float blend = (1 - SmoothStep(0,1,InverseLerp(25,65,dx)));   // 25 m 外完全回到原始平地
```
意思是：**只有路（±2.5 m）按路线升起，两侧 25–65 m 又平滑回落到原来那片平地**。
所以视角上看到的是"平地上一道土埂 + 几段台阶"，而不是"整片山坡在上升"。

而路线本身确实在爬：实测 `seg2 Δy 8.3 m / 59 m ≈ 14%`、`seg3 Δy 10.1 m / 62 m ≈ 16%`，总爬升约 25–29 m。

## 目标
**让路两侧的地面一起抬起来**，形成一整面上坡的雪坡；路只是嵌在坡上的一条带台阶的小径。

## 做法
1. `TerrainHeight` 里把"回落到原始平地"的混合半径从 **25–65 m** 扩到 **60–140 m**，
   并且**不要把 route 高度减掉**：在 dx=2.5→60 m 区间里，让地形高度从 `route` 平滑过渡到
   `route + 坡肩`，而不是过渡回 `original`（平地的 old height）；
2. 坡肩高度：**横向每 10 m 抬升 0.8–1.5 m**（近似 8–15% 的侧坡），到 60–90 m 处与远山脚自然衔接；
3. 保留 `z >= -40f` 的教堂区域不变（台地不能被改）；
4. 台阶、路面、围栏、脚印的贴地与避障逻辑不变（它们本来就按 `TerrainHeight` 采样）；
5. 允许在坡上撒几处**裸露岩脊**（0.3–1.2 m 高、2–6 m 长，灰石材质）作为"坡在上升"的视觉锚点。

## 验收
- `[SLOPE] at_z=-150 rise_lateral10m=…; at_z=-95 =…`，要求横向 10 m 处的地面比路心**高 0.8–1.5 m**；
- `[SLOPE] field_rise`：路径起点（z≈-205）与终点（z≈-40）之间，**距路心 30 m 处**的地面高度差 ≥ **18 m**
  （现在这个差值应接近 0）；
- 截图：`climb_slope.png`（侧后方机位，能看出整片雪坡在上升）、`play_view.png`、`climb_steps.png`；
- `-demo-walk 6` 仍走完（≈17.7 m）、`bad=0`；入堂与 reveal 退出码 0；
- 室外 `fps ≥ 200`；
- **不要求 rebuild_all 全绿**（用户已免除）。

## 硬约束
- 不动教堂、台地墙、围栏、台阶几何、相机、雾、室内、人物。
