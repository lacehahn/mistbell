只做一件事：把教堂做成**可进入**——内部打通、门可进、室内成空间、室内可交互。这是外观完成后的下一阶段主体。

必读：`spike/tools/build_church.py`（Blender 建模脚本）、`art-ref/sheet-v2-church-hd2d.jpg`（室内平面/剖面：净高 12.0 m）、`Assets/Scripts/PoiData.cs`、`docs/03-flow.md`（Z06 广场与内部）。

【第一步 Blender：把体块挖空】
1) nave（长 16 × 宽 9）从实心块改为**墙体壳**：墙厚 0.6 m、**室内净高 12.0 m**（与设定画一致），地面在 y=0。
2) transept（长 14 × 宽 7）与 apse（后半圆 r3.2）同样挖空并连通 nave，形成十字内部空间。
3) 正门：把门洞做成**真实通道**（洞口 2.6 宽 × 3.6 高，穿透墙厚），门扇改成一扇**可开合的木门**（单独对象 `Door_Leaf`，铰链在左，可绕 Y 轴旋转 100°）。
4) 侧墙 3+3 个拱窗改成**真实开洞**（可透光），玻璃面单独对象（`Window_Glass_*`），便于 Unity 侧做暖光。
5) 室内构件（低模即可，全部单独命名）：
   - 石柱 4 根（r0.35 h12，位于 nave 四角内侧）
   - 长椅 6 排（每排 3.6×0.45×0.85，两侧各 3 排）
   - 圣坛桌 1（2.2×0.9×1.0，位于 apse 前）
   - 烛台 4（柱状 0.25×1.4）
   - 吊灯 1（环状 r1.2，挂在 nave 中央 y=8.5）
   - 圣像屏 1（apse 前 3.2×0.15×4.5）
   - 旗帜 2（1.2×3.0，蓝金/红金）
6) 导出：`spike/export/gergeti_church.fbx` 覆盖；**室内外合并后三角形 ≤ 95,000**（外观 73,674 + 室内约 2 万）。

【自检打印（Blender 侧）】
"[INTERIOR] shell_wall=0.6 nave_clear_h=12.0 door_leaf=true window_openings=6 columns=4 pews=6 altar=1 candles=4 chandelier=1 iconostasis=1 banners=2 tris=<n> bbox=<w>x<h>x<d>"

【第二步 Unity：能走进去 + 室内氛围 + 交互】
1) 门：玩家进入 2.5 m 半径按 E → 门绕铰链开 100°（0.8 s），保持开启；门开着时洞口可通行（门的碰撞体随开合移动或直接关闭碰撞）。
2) 室内碰撞：墙、柱、长椅、圣坛桌、圣像屏都要挡人；地面可行走（与室外同一 step-up 逻辑）。
3) 室内光照：4 支烛台的暖点光（半径 4 m、强度 1.6）、1 盏吊灯（半径 8 m、强度 1.2）、窗洞透进来的冷光柱（3–4 条半透明锥形片，朝地面）、整体环境光比室外更暗（sky 亮度 ×0.6）。
4) 室内相机：进入室内后 FOV 42 → **32**，跟随偏移 `(0,3.2,−7.5)`，避免穿墙（沿用现有 SphereCast 拉近）。
5) 室内 POI（数据驱动，沿用 `PoiData`）：
   - `gergeti_p04` 圣像屏 · Iconostasis（锚点 apse 前）
   - `gergeti_p05` 烛台 · Votive Candles（锚点左侧烛台）
   - `gergeti_p06` 壁画残片 · Wall Painting（锚点 nave 右墙）
   文案：每家中文 4–6 行（显式 \n）、英文 1 段，口径与现有 3 个点位一致；内容围绕"14 世纪、修士、风雪封山、朝圣者留痕"。

【自检打印（Unity 侧）】
"[INTERIOR_U] door_open_deg=100 walk_in=true player_inside=true fov_in=32 lights=5 light_shafts=<n> colliders=<n> pois=3"
"[INTERIOR_U] screenshots=Shots/interior_entry.png,Shots/interior_inside.png,Shots/interior_poi_p04.png"

【必须自己跑】
新增演示参数：
1) `-demo-enter-church`：从底座前走到门口 → 开门 → 走进去 → 截图 `interior_entry.png`（门口视角）与 `interior_inside.png`（室内中殿）。
2) `-demo-poi gergeti_p04`：室内 POI 面板截图 `interior_poi_p04.png`。
硬门槛：`player_inside=true`（玩家 x∈[−4.5,4.5]、z∈[−9,7]、y 在室内地面 +0.3 内）、门开度 ≥ 95°、三张截图存在且非空。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-cinema`、`capture_runtime.sh`、`measure_shot.py`（magenta_px=0）全部保持通过。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改外观机位表/远山/雪面/measure_shot.py。
