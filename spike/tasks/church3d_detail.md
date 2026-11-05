# 任务 B：3D 教堂细活（砌块 / 屋顶 / 窗）

## 现状
教堂整体形体已成立，但近看：砌块只有 3 档明度、灰缝宽度均匀；屋顶瓦片排列整齐到失真；
窗只是发光的方块，没有窗棂投影与内部亮斑。

## 要做的（只改 3D 教堂本体 + 它的材质）

### 1. 墙面砌块
- 明度分层从 **3 档 → 4 档**（同色相，`b*` 不变）；
- 灰缝宽度 **±40% 随机**（现在等宽），缝色比块面暗 18–22%；
- 每 6–9 块出现一块**稍大的条石**（长 1.4–1.8 倍），打破节奏。

### 2. 屋顶
- 瓦片行间距 **±2% 随机错位**，每 5–7 行加一条**檐口阴影**（暗 12%）；
- 屋脊与山墙脚各加深一层投影（alpha 0.35、宽 6–10 px 等效）。

### 3. 窗户
- 窗棂（竖 1 / 横 2）加 **0.15 m 投影**，落在窗洞内侧；
- 玻璃加一块**梯形亮斑**（比当前自发光亮 20%，位置偏上内侧），模拟室内光；
- 窗洞内壁暗化 8%，加强进深。

## 验收
- `[CHURCH3D] block_levels=4 joint_var≥0.4 joint_dark=0.18–0.22 long_stone_every=6–9`
- `[CHURCH3D] roof_jitter=0.02 eave_shadow_every=5–7 window_mullion_shadow=true glass_glow=true`
- 截图：`art_runtime.png`（全景）、新增 `church_closeup.png`（距教堂 12 m，正面）
- 室外 `fps ≥ 200`、`drawcall ≤ 400`；`-demo-walk 6`、`-demo-enter-church`、`-demo-reveal` 均 `bad=0`
- 不动 2D 攀爬、不动相机、不动室内、不动路面与围栏

## 收尾（必须）
- commit + `git tag -f checkpoint-church3d`
- 更新 `docs/12-handoff.md`：勾掉 B，写上新的下一步
