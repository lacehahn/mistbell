# PROGRESS — Mistbell 自主推进日志

> 规则：每完成一步就在「日志」里追加一行（时间 | 步骤 | 命令/产物 | 结果）。任何新会话先读本文件末尾 3 条 + `git log --oneline -5` 再动手。
> Codex 限流时：在日志记「Codex 限额耗尽，等待重置」，本步跳过，下一次调度再试；**不要**因此中断其它不依赖 Codex 的工作。

## 当前状态（最后更新：2026-11-01 23:46）

- 环境：Unity Hub 3.22.5 / Unity 6000.3.27f2 / Windows Mono 模块 / macOS 目标内置 / Blender 5.2.3 LTS / Unity Personal 已激活
- 闭环已验证：
  - `-batchmode -executeMethod SpikeBuild.Run` → macOS 114.2 MB、Windows 96.2 MB，均 `Succeeded / 0 errors`
  - `-batchmode -executeMethod SpikeScene.Run` → 建场景 + 落编辑态参考图
  - `caffeinate -dimu -t 150 bash tools/capture_runtime.sh` → 播放器按 `ShotRig` 机位表逐张出图（多机位一次出包）
- **⏰ 运行时截图必须先 caffeinate**：显示器休眠时 Unity 播放器不会推进主循环（日志停在 `UnloadTime`，进程 0% CPU），截图会「静默失败」而不是报错。cron 夜间跑尤其容易踩。
- **⏰ 截图必须预热 1.0 s**：只等 3 帧会抓到窗口「合成/淡入中」的帧，整帧均一压暗（连天空都暗），同一份构建两次亮度差近 2 倍（sky 均值 139 / 75 / 100）。改成 `WaitForSecondsRealtime(1.0f)` 后三次连拍 sky 均值完全一致 (166,182,202)。
- 场景几何约定（后续沿用）：坡地 4°、`GroundY(z)=tan4°·z`（z=0 处 y=0）；教堂整体抬高 `ChurchBaseY=0.6`；**门廊朝 -z（来路侧）**，玩家自下坡上来看到的是正立面。
- **✅ 截图现在是可复现的测量工具**：`tools/frame.py`（原 pngstat.py）可直接对 PNG 求区域均值/削顶率，曝光改动用它验，不要只靠肉眼。
- 材质约定（后续沿用）：
  - 所有网格 UV 以**米**为单位，材质 `_BaseMap` tiling = `1/每张贴图覆盖的米数`（草 3.2 m、石 2.4 m、瓦 2.4 m）；**不要**用 `PrimitiveType.Cube`（每面 0..1 UV，不同立面的纹理密度会差 1.4 倍）。
  - 贴图导入：Point 过滤 + 无压缩 + Repeat + mipmap + `isReadable`。
  - `_BaseColor` 不是美术色，而是**反照率归一系数**（`SpikeArt.TintFor`）：贴图 sRGB→线性后会暗 2~3 倍，必须按目标反照率反推；守卫用 **p99**（允许 1% 高光削顶），用最大值会把整片草地压黑。
- 机位表在场景里的 `ShotRig`（`Assets/Scripts/ShotRig.cs`）：`art_runtime`（reveal，pitch 16/d 50/yaw 40）、`art_close`（pitch 14/d 14/yaw 12）、`art_ground`（贴地近景，pitch 26/d 7/yaw 15）。
- 已知问题：像素角色在画面中比预期小——图集格子 64×80 里角色本体只占约 45% 高度，实际屏高约为 quad 的 0.45 倍（放队列 5 一起处理）
- 美术目标（用户 2026-11-02 指示）：**尽可能实现类似《歧路旅人》的 HD-2D 风格**

## 待办队列（按顺序，未完成的在前）

1. ~~**构图修正**~~ ✅ 已完成（2026-11-01 23:20）：塔顶 y=95px 余量、基座 y=859px、朝圣者 y=921px 入画
2. ~~**曝光与光照**~~ ✅ 已完成（2026-11-01 23:21）：sun 1.5 / ambient 0.45 / postExposure 0.4 / bloom 0.9 / sun yaw +25（先试 2.2 过曝，回调）
3. ~~**程序化像素贴图**~~ ✅ 已完成（2026-11-01 23:46）：`tools/make_textures.py` 出草/石/瓦三套（128×128 可平铺 + 法线图），`SpikeArt.cs` 米制 UV 盒体 + 反照率归一；近景 `art_ground.png` 可见草叶颗粒与石块起伏。**遗留**：石墙偏白、缺脏污/AO 渐变（并入队列 4 的光照一起调）
4. **雾的分层**：3 层半透明雾片（近/中/远，不同密度与色温）+ 现有线性雾，制造纵深。
   - 附：石墙仍偏"干净"，同批加 SSAO（RendererFeature）+ 环境色调，把「积木感」压下去
   - 附 2（2026-11-02 新增）：远山三层现在是**平涂纸片**（Unlit 纯色、无雾衰减、白白一道雪线描边），必须与雾同批处理
5. **角色动画与朝向**：3 帧循环 + 按相机方位切换 down/up/left/right；顺带修「角色在图集里偏小」的问题
   - 现状（2026-11-02）：角色屏高已达标（14.5%，略高于目标 10–13%），但放大后读起来像「深色木桩」——图集本体只占格子 45% 且配色太暗，必须重画图集而不是继续放大 quad
6. **带状移轴 RendererFeature**：URP 的 Bokeh DoF 是体积式，不是《歧路旅人》那种上下带状遮罩；需自定义 Pass（排除角色与 UI）
7. **D03 的 12 张 A/B 截图**（低透视 vs 正交 × 6 机位 × 关/开后期）——`ShotRig` + `-shots` 机制已就绪，直接扩机位表即可
8. ~~**Codex D1**：用 Blender 建真正的教堂外壳替换体块~~ ✅ 已完成（2026-11-02 00:31）：`tools/build_church.py` → FBX → `ImportChurch()`
9. **补材质与坏机位**（小活，下一轮先做）：
   - 台地/挡土墙是**纯色未贴图**的板（`art_close.png` 下三分之一那片平涂米色），要套 `M_Stone` 的米制 UV
   - `art_ground` 机位落在挡土墙里（整帧平涂、削顶 48%），要挪到草地上
   - 教堂正面被阳光直打到近削顶（`art_close` 削顶 23%），与雾/曝光同批回调

## 日志

| 时间 | 步骤 | 命令/产物 | 结果 |
| --- | --- | --- | --- |
| 22:04 | 装 Hub+Blender | `brew install --cask unity-hub blender` | 成功（Blender 5.2.3 LTS） |
| 22:14 | 装编辑器+模块 | Hub headless install 6000.3.27f2 | 成功，10 GB，Windows Mono 977 MB |
| 22:15 | 激活授权 | 用户登录 Hub | Unity Personal，`UnityEntitlementLicense.xml` |
| 22:16 | 建工程 | `-createProject spike/project` | exit 0 |
| 22:17 | 出双平台包 | `-executeMethod SpikeBuild.Run` | macOS 102 MB / Windows 86 MB，0 errors |
| 22:17 | 首帧截图 | `-executeMethod SpikeCapture.Run` | PNG 51 KB 落盘，渲染正常 |
| 22:20 | 标尺复测 | 同上，标尺右移 4.5 m | 149.1 px（仍与 172 有差，待查） |
| 22:30 | 立项美术目标 | 用户指示 HD-2D 风格 | 写入本文件待办队列 |
| 22:32 | 建定时任务 | `hermes cron create` ×2 | 6ee19f5fb8b1 每 30 分钟推进（local）；e7c17249f0e9 每天 08:00 汇报（origin） |
| 22:33 | 装 URP | `add_urp.py` + `SpikeURP.Run` | URP 17.0.1 + ShaderGraph 入 manifest（37 包）；`Assets/Settings/MistbellURP.asset` + `MistbellRenderer.asset` 建成并设为默认 |
| 22:34 | 生成像素角色 | `tools/make_sprites.py` | `Assets/Art/Character/pilgrim.png` 192×320（4 方向 × 3 帧，64×80/格，纯标准库写 PNG） |
| 22:34 | 写 Billboard 着色器 | `Assets/Art/Shaders/PixelBillboard.shader` | Y 轴锁定 + alpha clip + UV 图集支持（URP HLSL） |
| 22:35 | 统一 Codex 口径 | `spike/tools/codex.sh` | 固定 gpt-6.1-sol + reasoning_effort=medium；定时任务 prompt 已同步 |
| 22:36 | 建 HD-2D 场景 | `SpikeScene.Run` | 坡地 + 体块教堂（主体/屋顶/钟楼/尖顶/门廊柱/楣）+ 像素角色 + 雾 + Volume（Tonemap/Bloom/DoF/调色/暗角/颗粒）；产出 `Shots/art_*_edit.png` |
| 22:37 | 发现后处理失效 | 目视 `art_wide_edit.png` | **编辑态 `cam.Render()` 不跑 Volume 栈**（无 bloom/移轴/暗角）→ 改为运行时截图 |
| 22:38 | 加运行时截图器 | `Assets/Scripts/Screenshotter.cs` | 播放器带 `-shot <path>` 时渲染 3 帧后 `ScreenCapture` 落盘并退出 |
| 22:40 | 重建+出包+运行截图 | `SpikeScene.Run` → `SpikeBuild.Run` → 播放器 `-shot` | 进行中（日志 `08/09/10_*.log`） |
| 22:44 | 运行时截图初测 | `capture_runtime.sh` | 播放器能跑能截（141 KB）；但**后处理仍未生效** |
| 22:46 | 诊断后处理 | `[SHOT]` 日志 | `profile= components=0` → VolumeProfile 的子组件没被打进构建包 |
| 22:48 | 运行时时重建 Volume | `Screenshotter.BuildRuntimeProfile()` | `runtime volume built, components=7` 后处理生效（暗角/暖调可见） |
| 22:49 | 编译错误 3 次修正 | `error CS0104 Object 二义` / `VolumeProfile 缺 using` | 加 `UnityEngine.Rendering`、全限定 `UnityEngine.Object` 后 build=0 |
| 22:50 | 目视评估当前画面 | `Shots/art_runtime.png` | 后处理 OK；但构图切掉塔顶、角色不在画面、整体偏暗、纯色块无像素贴图 → 已写入队列 1–4 |
| 23:05 | 构图诊断（量化） | 投影脚本 `scratch/fix1.py`（按教堂包围盒求机位） | 查出两个真 bug：①旧机位 target=(0,4,0)/pitch22/dist34 使塔顶出画；②朝圣者在 z=8.5 被教堂体块**完全遮挡**（视线在 z=4.5 处 x=-2.9,y=3.0 命中体块），所以「角色不在画面」其实是「被挡住」 |
| 23:10 | 场景几何修正 | `SpikeScene.cs`：坡地改 4° 且 `y=0@z=0`、教堂抬高 0.6、**门廊翻到 -z（来路侧）** | 教堂不再悬空/埋地；玩家自下坡来能看到正立面 |
| 23:12 | 多机位机位表 | 新增 `Assets/Scripts/ShotRig.cs` + `Screenshotter -shots <dir>` | 一次出包拍 N 张；日志 `[SHOT] pose[i]` 逐个确认 |
| 23:15 | 运行时截图卡死排查 | 播放器日志停在 `UnloadTime`、进程 0% CPU、`pmset -g assertions` 显示显示器即将休眠 | **显示器休眠 → Unity 播放器主循环不推进**；改 `caffeinate -dimu -t 120 bash tools/capture_runtime.sh` 后正常出图 |
| 23:20 | 构图定稿（队列 1 ✅） | `SpikeScene.Run` → `SpikeBuild.Run` → `capture_runtime.sh` | 塔顶 y=95px（留 95px 头顶空间）、基座 y=859px（下方留 ~220px 前景地面）、朝圣者 y=921px 入画 85px 高；产物 `Shots/art_runtime.png`、`Shots/art_close.png` |
| 23:22 | 曝光/光照（队列 2 ✅） | sun 1.6→2.2→**1.5**（2.2 把正对光石墙打到纯白 clip）、ambient 0.30→0.45、postExposure 0.2→0.4、bloom 1.0→0.9、sun yaw -35→**+25**、石色 0.72→0.66 | 石墙有中间调与两档明暗面，无过曝；两次重跑日志 `logs/2[2345]_*.log` |
| 23:23 | 目视判定（HD-2D 像不像） | `vision_analyze` on `art_runtime.png`/`art_close.png` | 构图与曝光合格；**离 HD-2D 还差三件事**：地面/石墙仍是纯色块（缺像素贴图）、无雾层纵深、无带状移轴 → 正是队列 3/4/6 |
| 23:34 | 程序化材质生成器（队列 3 起） | `tools/make_textures.py`（纯标准库：值噪声 + PNG 编码 + 由高度场推法线） | 出 `Assets/Art/Textures/{grass,stone_ashlar,roof_slate}[_n].png` 共 6 张 128×128；平铺自检 seam(列,行) 草 0.88/1.04、石 0.12/0.02（≈1 表示接缝与内部无差别）。石砌改「随机块宽 + 错缝」后接缝比从 1.75 降到 0.12 |
| 23:36 | 米制 UV 盒体 + 材质导入 | 新增 `Assets/Editor/SpikeArt.cs`（`MeterUVBox`/`Box`/`ImportTex`/`MakeTextured`） | 放弃 `PrimitiveType.Cube`（每面 0..1 UV → 正/侧立面纹理密度差 1.4 倍），改为 UV 写入米数、tiling=1/米数；导入 Point+无压缩+Repeat+mipmap |
| 23:37 | 首轮渲染（踩 2 个坑） | `SpikeScene.Run`→`SpikeBuild.Run`→`capture_runtime.sh` | ①盒体忘设 `localScale=size` → 全部变 1 m 小方块、地面消失、纹理被 mip 抹平；②`_BaseColor` 按通道补偿把 grass 蓝通道放大 6.2× → 反照率 >1 削顶，整帧发白（削顶率 0%，但近景纯白）。日志 `logs/2[68]_*.log` |
| 23:39 | 修反照率归一 | `SpikeArt.TintFor`：改「按亮度统一缩放 + p99 守卫」 | 纯峰值守卫把草地压到 guard=0.43（albedo 0.10/0.15/0.04，草地发黑）→ 改 p99 后 guard≈0.98；但草/瓦仍过饱和（霓虹绿/番茄红） |
| 23:41 | 反照率改逐通道 + 重标定曝光 | `TintFor` 逐通道；目标 albedo 石 0.36/0.34/0.30、瓦 0.24/0.14/0.11、草 0.12/0.16/0.07；sun 1.2、ambient 0.40、postExposure 0.2、contrast 14、bloom 0.75 | 削顶率 0%、天空均值 166；但发现**同构建两次截图亮度差近 2 倍**（sky 139/75/100）→ 曝光标定不可信 |
| 23:42 | 修截图不确定性 | `Screenshotter`：预热改 `WaitForSecondsRealtime(1.0f)`；并保证全局只有一个生效 Volume（其余 `enabled=false`） | 三次连拍 `sky` 完全一致 (166,182,202)、`ground_near` (136,156,112) → 截图从「碰运气」变成可复现测量。根因是抓到窗口合成/淡入中的帧（整帧均一压暗，连天空都暗） |
| 23:43 | 量化工具 | `tools/frame.py`（纯标准库 PNG 解码，求区域均值/最大值/削顶率） | 曝光与材质改动一律用它验，不再只靠肉眼 |
| 23:44 | 加贴地近景机位 | `ShotRig` 增 `art_ground`（pitch 26 / d 7 / yaw 15） | 用于量「草地像素密度」：草叶约 4~6 px/颗、无可见平铺网格 |
| 23:46 | 材质定稿 + 目视（队列 3 ✅） | `make_textures.py`→`SpikeScene.Run`→`SpikeBuild.Run`→`capture_runtime.sh` | 石块块间色调差 ±0.15（±0.24 时像拼花被面，回调）；macOS 114.2 MB / Windows 96.2 MB、0 errors。产物 `Shots/art_runtime.png`(2.2 MB)、`art_close.png`、`art_ground.png`；阈值：削顶 0%、天空 166、地面近 (136,156,112)。**判断**：已脱离纯色块，草/石/瓦都能读出材质，但仍缺「带状移轴 + 雾层纵深 + 可辨认的角色」三件 → 队列 4/5/6 |
| 23:58 | Codex 修构图与曝光（人工派发） | `spike/tools/codex.sh`（gpt-6.1-sol/medium） | exit 0；只改 SpikeScene.cs/Screenshotter.cs；3 条验收命令全 0；`components=7`；Codex 自报"角色高于坡面" |
| 00:00 | 人工验收（读图） | `Shots/art_runtime.png` | 教堂全高入画 ✓、曝光提亮 ✓、像素材质已上（草/砖/瓦）✓；**角色悬空** ✗、无雾无纵深 ✗、调色偏灰 ✗ |
| 00:03 | 收入概念图参考 | 用户提供 Gergeti 晴日概念图 | 存为 `art-ref/concept-gergeti-sunny.jpg`，规格写入 `art-ref/notes.md`（构图/光照/色板/待修 5 项） |
| 00:05 | 派发 Codex 第 2 轮 | `codex.sh -i art-ref/concept-gergeti-sunny.jpg` | 任务：角色地形吸附、3 层远山、石板路、暖补光、色板对齐；进行中 |
| 00:20 | 收入三张目标风格参考 | 用户提供设定画 + 两张成片 | 存 `art-ref/concept-sheet-hd2d.jpg`、`target-front-hd2d.jpg`、`target-path-hd2d.jpg`；规格写入 `art-ref/notes-winter-hd2d.md` |
| 00:21 | 关键发现 | 设定画标注尺寸 | **教堂真实尺寸 34 m 宽 × 27 m 高 × 24 m 深**（此前按 14 m 建，是构图反复切顶的根因）；穹顶 12 边形、钟楼 8 边形开敞且独立 |
| 00:28 | 验收 Codex Blender v1 | 独立跑 `check_church.py`（blender -b） | **不合格**：bbox 40.0×34.5×25.4 超限、tris=1560、拱门未开洞（前墙仅 88 tris）；根因含我的 spec 自相矛盾（台地40×32+侧屋30m+钟楼外偏塞不进 34×24） |
| 00:30 | 重发 Blender v2 | `codex.sh`（自洽尺寸 + 三视图渲染 + 硬门槛） | 门槛：bbox_in_range=True、tris≥3000、nave_front_tris≥120、产出 views/{front,side,top,iso}.png；进行中 |
| 00:38 | 验收 Codex Blender v2 | 独立 `check_church.py` + 看三视图 | **通过**：bbox 34.00×24.00×26.60（in_range=True）、tris 14864、前墙 944 tris（拱洞真切）；三视图 front/side/top/iso 已出；评：正立面缺拱窗、穹顶偏矮胖、檐口太厚 |
| 00:40 | 派发 Unity 导入+相机重算 | `codex.sh`（第 5 轮） | 关键计算：相机距角色 20 m（≈150 px）、教堂推至 110 m（约 39% 画面高），解决"真实尺度下角色与教堂比例不可兼得"；硬门槛 35–45% / 120–170 px / tris>5000；进行中 |
| 00:47 | Blender 教堂接入 Unity（D1 ✅） | `codex.sh` 读 `spike/brief-church-massing.md`；`SpikeScene.ImportChurch()` 载 FBX | 弃用 14 m 盒体，改 Blender 模型（12 边穹顶 + 8 边钟楼 + 台地）；地形扩 200×260、坡 6°、石板路 12 段；机位改**贴地平视** `EyeAt=地面+1.9 m`；新增 `[SPIKE] frame` 自检日志。人工独立复跑：`frame church heightPct=54.4 pitch=13.0 pilgrim=10.8 ridge1/2/3=33.8/35.8/28.1`，EXIT=0、双平台 Build Success（109 MB / 92 MB）、三机位出图。commit `4185326` |
| 01:00 | 正面立面：山墙/门廊/台阶/窗（D2 ✅） | `codex.sh` 读 `spike/brief-facade.md`（**超时 420 s 被杀**，但文件已落盘） | 人工补验收：`build_church.py -- --verify` VERIFY=0（gable + porch 6×1.8×5.5 + 两柱 + 5 级 0.28×0.45 台阶 + 7 盏内凹窗 + 钟室 8 拱洞，全 UV 有限）；FBX 547 KB→1.8 MB；`frame church heightPct=49.8 widthPct=34.4 pitch=11.8 porch heightPct=10.0 centerX=924`；EXIT=0、Build Success ×2。**教训：codex.sh 只在 420 s 内有效，任务大于此必须拆或改后台轮询** |
| 01:03 | 目视判定（HD-2D 像不像） | `vision_analyze` on `art_runtime/art_close/art_ground` + `blend/views/front.png` | **形对了**：山墙三角、双柱拱门廊、凹进门洞、立面拱窗、8 拱钟室、台地与台阶，一眼能认出是格鲁吉亚教堂；平视机位（pitch 11.8°）与参考图气质接近。**还差**：① 远山是平涂纸片 + 白描雪线，无雾无纵深；② 台地/挡土墙**纯色未贴图**（近景下三分之一一片平涂米色）；③ `art_ground` 机位掉进挡土墙（整帧平涂、削顶 48%）；④ 正面被阳光直打，`art_close` 削顶 23%；⑤ 角色像深色木桩；⑥ 无雪（目标风格是冬季） |
| 00:52 | 验收 Codex 第 5 轮（Unity 导入） | 读 50_scene.log + 自行补跑 | 数值全过：church tris=52384、bounds 34.00×26.60、占比 48.8%、pilgrim 156.5 px 且脚踏地形(-8.53=-8.53)、材质 stone37/roof6/wood8 |
| 00:55 | **发现交付缺陷** | 运行 `capture_runtime.sh` | 运行时日志 `fov=28`，而脚本里 `Fov=35` → **场景里存在多个相机，`Camera.main` 抓到的不是揭示机位那台**；且 build 前必须先跑 SpikeScene.Run 重生成场景，否则用的是旧场景 |
| 00:56 | 定性 | 两次 build 失败(exit 1)原因 | 竞态：僵尸 Unity 进程残留锁；重跑即成功（日志只有 28 行、无编译错误即此特征） |
