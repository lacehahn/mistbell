# 室内第五轮：壁柱、穹顶、王门金框验收

仅执行 [`interior_pilasters_dome.md`](../../tasks/interior_pilasters_dome.md)。最终独立验收 **30/30 通过**，`accepted=true`。未提交；未修改 AGENTS.md、任务书及其他任务。

## 交付与参数

- 新局部入口 `InteriorPilastersDomeBuild.Run`，不重建场景。仅将 `Interior_Column_03/04` 的旧纹理材质替换为独立石材，取消铜黄色纹理；与墙共用石材色族，使用纵向凹槽、真实法线与受光表现体积。柱头/柱脚新增 0.16m 高浅石线脚，材质亮度比柱身高9%。原柱网格、变换、全部碰撞不改。
- 穹顶使用世界坐标平滑三维程序噪声，格尺度0.9m，调制系数 `1 + 0.04 × (2n − 1)`。±4%是着色器对反照率和自发光的标称调制范围，不将渲染图中的整体照明变化冒充噪声幅度。
- 原肋由两个共面半宽条组成；改为完整0.12m顶面和两片真实0.05m侧面。顶面材质相对抹灰+11%，侧面相对顶面−16%。为保留既有穹顶整体亮度上限0.28，抹灰底色/自发光统一为原值×0.945，再叠加斑驳；未改灯光或旧门槛。
- 12扇鼓座窗的自发光设为 `#D9E6F2 × 0.95`，每扇增加1根0.03m竖棂。上沿黑块实为 `Interior_DrumRib_0..11` 的旧0.18m方截面端部，并非随机穿模。保留装饰肋，截面收至0.04m、改用鼓座墙石材；旋转后的最大横向AABB为0.05464m，低于0.06m。
- `M_InteriorV2Gold` 原来还被两侧圣像拱框共用。只为 `Interior_RoyalDoor_Frame` 建立 `M_InteriorV2Gold_RoyalDoor` 副本，底色和自发光RGB各乘0.93；两侧拱框继续使用原材质。没有调整王门或圣像几何。
- 三束原光柱和地面光斑仍可见，保留原alpha=0.08；任务允许的0.10–0.14补偿没有启用。每束分别保存开启/关闭控制图，确认其画面贡献。

## 实测数字

| 项目 | 最终值 | 口径 |
|---|---:|---|
| 壁柱与后墙色差 | 0.7232 ≤ 3 | PNG CIELAB Δab，亮度单独验收 |
| 色相差 | 2.3890° ≤ 3° | CIELAB atan2(b,a)，圆周最短差 |
| 柱身L* / b* | 31.8819 / 10.2619 | 两根柱全部实际可见像素均值，6372个采样 |
| base_L | 33.5494 | 柱身最低可见段（世界y<3.5m），2242个采样 |
| 石线脚对比 | +9.00% ≥ 8% | 材质底色Rec.709比值；自发光同倍率 |
| 斑驳幅度 / 尺度 | ±4% / 0.9m | 程序参数，平滑三维噪声 |
| 肋顶面增益 / 侧面降幅 | +11.00% / −16.00% | 材质Rec.709；侧面以顶面为基准 |
| 肋宽 / 厚度 | 0.12000 / 0.05000m | 12根真实网格投影，min=max |
| 竖棂 | 12 × 0.03000m | 真实网格投影，min=max |
| 鼓座窗L* | 69.9341 | 抬头PNG、第一可见表面掩码，1982个采样 |
| 超标黑块 | 0 | 旧装饰端部同石材且横向AABB≤0.06m |
| 金框亮度（既有近景） | 0.53981 ≤ 0.55 | 既有PewsCloseup机位，左竖边下半段，显示Rec.709 |
| 金框亮度（原机位） | 0.45266 ≤ 0.55 | 同一测量口径 |
| 光柱 | visible=True，count=3 | 分别关闭三束的控制图，alpha保持0.08 |
| 室内性能 | 250 drawcalls / 217.84 FPS | Apple M6 / Metal，1920×1080前台Development，300帧 |
| 1% low / 显存 | 160.69 FPS / 489.87MB | 附加记录，不替代平均FPS门槛 |

**测量边界：** 原机位的实际柱脚线脚被长椅遮住，柱头线脚不在原机位画幅中。因此 `base_L` 是最低可见柱身段，不声称测到隐藏柱脚；线脚的9%对比使用序列化材质比值。柱身颜色以全部可见上/下段加权均值验收，原始分区数值保留在JSON中。屏幕颜色使用480×270第一可见不透明表面射线掩码，对1920×1080交付PNG相应像素独立重算；透明光柱保留其真实叠加贡献，隐藏几何不参与采样。

## 不回退检查

- 墙面受光L*=37.8189、背光L*=22.7298、差值=15.0891；b*=10.9673/8.1010。天花板L*=16.0468；王门内墙L*=28.8766。原墙面、分层及色相门槛全部通过。
- 原0.03m吊杆、3节链、禁用横梁和移除红板均未触碰。地板、室外、相机、祭坛/圣像、长椅和全部碰撞前后SHA256摘要一致。王门几何也纳入保护；只允许其材质替换。
- 同一局部修补连续执行两遍，所有授权对象的几何/变换/材质摘要一致（idempotent=true）。材质同步写入URP的旧版 `_Color` 镜像，避免保存后自动同步造成假差异。
- Unity构建 `Succeeded / errors=0`；`-demo-enter-church` 的 `[INT_PASS]` 所有布尔项为True，`obstacles=True lamps=20 shafts=3`；穹顶整体显示亮度0.2785，保留原0.20–0.28阈值。
- 原机位与抬头图均1920×1080、生成时间晚于本轮构建，并已目视复核。`git diff --check`通过。

## 复现和证据

```bash
bash spike/tools/accept_interior_pilasters_dome.sh
# 已完成同一次构建，仅重跑播放器/性能/验收：
bash spike/tools/accept_interior_pilasters_dome.sh --player-only
# 仅独立重算既有本轮数据：
python3 spike/tools/verify_interior_pilasters_dome.py
```

- [全部实测与30项检查](measurements.json)、[保护摘要](protected_digests.json)。
- 日志：`spike/logs/interior_pilasters_dome_{build,interior,bench,acceptance}.log`；性能原始数据：`spike/logs/interior_pilasters_dome_bench.json`。
- 截图：[原机位](../../project/Shots/interior_inside.png)、[抬头](../../project/Shots/interior_lookup.png)、[王门近景](../../project/Shots/royal_door_closeup.png)。
- 控制图：`spike/project/Shots/pilasters_{with,without}_Interior_LightShaft_{0,1,2}.png`。每束开启减关闭图的正增量采样分别为87870, 44786, 32616个，平均正增量分别为0.03450, 0.02769, 0.02296（显示Rec.709）。仅据此确认仍可见，不将其当作光通量或地板材质变化。
