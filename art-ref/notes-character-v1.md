# 主角规格 v1（依据用户提供的两张角色设定画）

参考文件：
- `art-ref/char-turnaround.jpg` — 1280×960，Original Character Design Sheet（正/侧/背三视图 + 色板 + 细节 + 表情）
- `art-ref/char-walk-sheet.jpg` — 1280×960，Production Sprite Sheet（Walk 动画 4 方向 × 6 帧 = 24 帧）

## 1. 身份

**The Pilgrim-Traveler**（朝圣旅人）：Wandering Adventurer / Pilgrim / Mountain Journey。

## 2. 造型要素（必须还原）

| 部位 | 描述 |
| --- | --- |
| 斗篷 | 蓝灰（蓝紫灰），下摆与门襟有**金色刺绣边**：朝圣纹样＝十字 + 山形图案 |
| 围巾 | 砖红/酒红，绕颈，尾端垂在背后 |
| 内衬 | 米白束腰外衣 + 棕色皮质腰封、腰带 |
| 挎包 | 棕色皮质 satchel，带十字压印、金属扣 |
| 靴 | 厚重旧皮靴，高筒，有搭扣 |
| 手杖 | 木杖，顶端弯曲，系**白色布条**（朝圣者标志） |
| 头发 | 蓬松棕发，略乱 |
| 胸饰 | 金色圆形别针（斗篷扣） |

## 3. 色板（从设定画取）

`#E8DFCF` 米白 / `#C9B79A` 浅沙 / `#8A6D52` 中棕 / `#6B4A32` 深棕 / `#4A3A30` 近黑棕
`#5B6472` 蓝灰（斗篷亮） / `#3E4654` 蓝灰（斗篷暗） / `#A35A4A` 砖红（围巾） / `#C9A227` 金（刺绣）

## 4. 帧组织（生产表）

- **Walk：4 方向 × 6 帧 = 24 帧**（Walk Down / Walk Left / Walk Right / Walk Up）
- 设定画另给：Idle(Front)、Walk、Run、Interact(Bend)、Rest(Sit) 五种姿态；表情 Neutral / Smile / Surprised

## 5. 切图与落地规格

- 源表 1280×960，3 行表头 + 4 行 × 6 列；每格约 **200×190 px**，实际角色需**按内容裁边**（不要按格子硬切，会带进标签和阴影）
- 输出图集：`Assets/Art/Character/pilgrim_atlas.png`，每帧统一为 **64×80 px**（与现有 64×80/格 一致），4 行（down/left/right/up）× 6 列
- 导入：Point filter、无压缩、Sprite Mode = Multiple，按 64×80 切片
- `PixelBillboard.shader` 需从 3 帧改 **6 帧**循环；朝向按相机方位角切 down/up/left/right
- 屏幕高度目标：近景机位 **120–170 px**、远景机位 **28–32 px**

## 6. 验收

1. 24 帧全部切出，无跨帧粘连、无标签残留
2. 每帧角色像素高度一致（±2 px），脚底对齐同一基线
3. 图集 PNG 的色板与设定画一致（偏差 ≤ 3 个像素色）
4. 运行时：四方向切换正确、6 帧循环无跳帧
5. 近景 120–170 px、远景 28–32 px
