# PROGRESS — Mistbell 自主推进日志

> 规则：每完成一步就在「日志」里追加一行（时间 | 步骤 | 命令/产物 | 结果）。任何新会话先读本文件末尾 3 条 + `git log --oneline -5` 再动手。
> Codex 限流时：在日志记「Codex 限额耗尽，等待重置」，本步跳过，下一次调度再试；**不要**因此中断其它不依赖 Codex 的工作。

## 当前状态（最后更新：2026-11-02 02:40）

- 环境：Unity Hub 3.22.5 / Unity 6000.3.27f2 / Windows Mono 模块 / macOS 目标内置 / Blender 5.2.3 LTS / Unity Personal 已激活
- 闭环已验证：
  - `-batchmode -executeMethod SpikeBuild.Run` → macOS 114.2 MB、Windows 96.2 MB，均 `Succeeded / 0 errors`
  - `-batchmode -executeMethod SpikeScene.Run` → 建场景 + 落编辑态参考图
  - `caffeinate -dimu -t 150 bash tools/capture_runtime.sh` → 播放器按 `ShotRig` 机位表逐张出图（多机位一次出包）
- **⏰ 运行时截图必须先 caffeinate**：显示器休眠时 Unity 播放器不会推进主循环（日志停在 `UnloadTime`，进程 0% CPU），截图会「静默失败」而不是报错。cron 夜间跑尤其容易踩。
- **⏰ 截图必须预热 1.0 s**：只等 3 帧会抓到窗口「合成/淡入中」的帧，整帧均一压暗（连天空都暗），同一份构建两次亮度差近 2 倍（sky 均值 139 / 75 / 100）。改成 `WaitForSecondsRealtime(1.0f)` 后三次连拍 sky 均值完全一致 (166,182,202)。
- 场景几何约定（后续沿用）：坡地 4°、`GroundY(z)=tan4°·z`（z=0 处 y=0）；教堂整体抬高 `ChurchBaseY=0.6`；**门廊朝 -z（来路侧）**，玩家自下坡上来看到的是正立面。
- **✅ 截图现在是可复现的测量工具**：`tools/frame.py`（原 pngstat.py）可直接对 PNG 求区域均值/削顶率，曝光改动用它验，不要只靠肉眼。
- **尺子清单**（都不许为了过门槛而改）：`frame.py` 曝光/过曝 · `edge.py` 地平线硬边（单行跳变 + 距离提亮）· `horizon.py` 地平线跨度 · `sharp.py` 三带高频（移轴）· `texstat.py` 贴图统计 · `region.py` 画面矩形均值（含 `p5/p50/tail`：判凹陷压暗而非整体变暗）· `probe_col.py` 逐列 RGB · `seam.py` 贴图**真实尺寸**环绕接缝 · `patchwork.py` 块级明度/色度离散（判「迷彩拼布」；**开了 SSAO 后 `blockSpread` 会被 AO 梯度污染，必须与 `neighborDiff` 一起读**）
- **SSAO 约定（2026-11-02 04:26 起）**：参数只写在 `SpikeURP.SSAO()`（`Intensity 2.2 / DirectLightingStrength 0.85 / Radius 0.5 m / Source=Depth`），资产 `Assets/Settings/SSAOFeature.asset` 是独立 `.asset`、被 `.gitignore` 忽略，**改参数必须改代码再跑一次 `SSAO`**。它只能压暗几何凹角（台阶/门廊/拱洞/墙-台地交界），**压不动贴图砖缝**。差分验证：`.asset` 里 `m_Active: 0` → 出包 → 截图 → 对比，是判断「AO 到底有没有生效」的唯一可靠办法。
- 材质约定（后续沿用）：
  - 所有网格 UV 以**米**为单位，材质 `_BaseMap` tiling = `1/每张贴图覆盖的米数`（草 3.2 m、**石 4.8 m**、瓦 2.4 m）；**不要**用 `PrimitiveType.Cube`（每面 0..1 UV，不同立面的纹理密度会差 1.4 倍）。石材 tiling 必须写在 `SpikeScene.cs` 里（`.mat` 被 `.gitignore` 忽略，写进 `.mat` 等于不可复现）。
  - 贴图导入：Point 过滤 + 无压缩 + Repeat + mipmap + `isReadable`。
  - `_BaseColor` 不是美术色，而是**反照率归一系数**（`SpikeArt.TintFor`）：贴图 sRGB→线性后会暗 2~3 倍，必须按目标反照率反推；守卫用 **p99**（允许 1% 高光削顶），用最大值会把整片草地压黑。
- 机位表在场景里的 `ShotRig`（`Assets/Scripts/ShotRig.cs`）：`art_runtime`（reveal，pitch 16/d 50/yaw 40）、`art_close`（pitch 14/d 14/yaw 12）、`art_ground`（贴地近景，pitch 26/d 7/yaw 15）。
- 已知问题：像素角色在画面中比预期小——图集格子 64×80 里角色本体只占约 45% 高度，实际屏高约为 quad 的 0.45 倍（放队列 5 一起处理）
- 美术目标（用户 2026-11-02 指示）：**尽可能实现类似《歧路旅人》的 HD-2D 风格**

## 待办队列（按顺序，未完成的在前）

1. ~~**构图修正**~~ ✅ 已完成（2026-11-01 23:20）：塔顶 y=95px 余量、基座 y=859px、朝圣者 y=921px 入画
2. ~~**曝光与光照**~~ ✅ 已完成（2026-11-01 23:21）：sun 1.5 / ambient 0.45 / postExposure 0.4 / bloom 0.9 / sun yaw +25（先试 2.2 过曝，回调）
3. ~~**程序化像素贴图**~~ ✅ 已完成（2026-11-01 23:46）：`tools/make_textures.py` 出草/石/瓦三套（128×128 可平铺 + 法线图），`SpikeArt.cs` 米制 UV 盒体 + 反照率归一；近景 `art_ground.png` 可见草叶颗粒与石块起伏。**遗留**：石墙偏白、缺脏污/AO 渐变（并入队列 4 的光照一起调）
4. **远山与雾的分层**（进行中，2026-11-02 01:25）：
   - ✅ 第 1 轮（Codex 6→7）：`RidgeFog.shader`（垂直渐变 + 山脚额外雾化 + 软雪线）、线性雾 55/650、`mist.png` + 两条雾带；机制跑通但**画面被洗白**（雪线高度按 110/250 m 判 → 整山是雪；fogEnd 650 让最远层 100% 融进雾）
   - ✅ 第 2 轮修正：fog 90/1500 + fogColor = 天空色 `#A7B7CB`；三层配色改深（#44546A→#A9B8C6 递浅）、雪线只留 300 m 以上；雾带按我的方向笔误挪到 **z=+170/+330**
   - 现状：三层山可数、都深于天空、无白描边；**雪几乎看不见**（第 2 层最高只 185 m）、**雾带在画面里读不出来**
   - ⚠️ 下一轮重点不是山，而是**地形远边**：坡地是 200×260 的平板，远端在 z=+100 直接断成一条笔直的天地交界线（`art_runtime.png` y≈760 px），远山浮在这条线后面 → 「桌面模型」感的主因
5. **角色动画与朝向**：3 帧循环 + 按相机方位切换 down/up/left/right；顺带修「角色在图集里偏小」的问题
   - 现状（2026-11-02）：角色屏高已达标（14.5%，略高于目标 10–13%），但放大后读起来像「深色木桩」——图集本体只占格子 45% 且配色太暗，必须重画图集而不是继续放大 quad
6. **带状移轴 RendererFeature**：URP 的 Bokeh DoF 是体积式，不是《歧路旅人》那种上下带状遮罩；需自定义 Pass（排除角色与 UI）
7. **D03 的 12 张 A/B 截图**（低透视 vs 正交 × 6 机位 × 关/开后期）——`ShotRig` + `-shots` 机制已就绪，直接扩机位表即可
8. ~~**Codex D1**：用 Blender 建真正的教堂外壳替换体块~~ ✅ 已完成（2026-11-02 00:31）：`tools/build_church.py` → FBX → `ImportChurch()`
9. ~~**补材质与坏机位**~~ ✅ 已完成（2026-11-02 01:06）：根因是 **UV 单位错**（`smart_project` 归一化 0..1 vs 材质约定「1 UV = 1 m」）→ `build_church.py` 改按面主导轴写入世界坐标（米）UV，`--verify` 新增 `UVSCALE OK` 断言；`art_ground` 挪到台地外草地；sun 2.2→1.8。削顶 art_runtime 1.41%→0.00%、art_close 23.20%→0.23%、art_ground 48.05%→0.10%
   - 遗留（2026-11-02 01:10 目视）：**门洞/窗洞内部是平涂棕**（`wood` 材质没贴图）；台阶与台地同材质且无 AO，读不出级差；石材 tile 2 m 下块体偏大、「太新」缺脏污渐变 → 并入队列 4 一起调
10. **wood 贴图 + AO 分离**（小活）：`M_Wood` 套一张 128×128 木纹/暗部贴图（`make_textures.py` 加一类），门洞内凹面与台阶侧面靠 AO/顶点色压暗，让台阶读得出 5 级
11. ✅ **SSAO（RendererFeature）** 已完成（2026-11-02 04:26）：`SpikeURP.SSAO()` 建独立 `Assets/Settings/SSAOFeature.asset` 挂进 `MistbellRenderer.asset`（features=2：TiltShift + SSAO），`Source=Depth`（`TerrainHaze.shader` 无 DepthNormals pass）、`Intensity 2.2 / DirectLightingStrength 0.85 / Radius 0.5 m`。
   - **关键认知（写给下一轮）**：SSAO 只能压暗**几何凹角**（台阶/门廊内凹/拱洞/墙与台地交界），**压不动贴图上的砖缝** —— 砖缝的接触阴影只能靠高度场倒角 + 法线。第一轮 `Intensity 0.75 / DLS 0.15` 几乎看不见（墙面 tail 只降 1.4%、平墙 0%），是因为 URP 默认 `AfterOpaque=false` 时 AO 主要作用在间接光上，而正立面被太阳直打。
   - 差分验证协议（以后照用）：`m_Active: 0` → `SpikeBuild.Run` → `capture_runtime.sh` → `region.py`，再对比。证据 `Shots/ssao_off_close.png` vs `Shots/art_close.png`。
   - ⚠️ 代价：`region.py` 的 `blockSpread` 从 21.4% 升到 **27.8%**（超过 24% 门槛）—— 这是 **AO 梯度**造成的矩形内亮度不均，不是块间色差（`neighborDiff` 仍 7.7% ≤ 10%）。**该尺子从此不能单独判「迷彩拼布」，必须与 `neighborDiff` 一起读。**
12. **地形远边收口**（2026-11-02 01:25 新增，优先）：坡地远端 z=+100 直接断成直线；要把地形做成「远端下沉/起伏 + 一层可见的地平雾」，否则无论山做多好都是「桌面模型」
   - 进展（01:48）：坡地换成程序化地形网格（X±150、Z -160..560、23103 顶点 / 45600 三角面），脊线 `crestZ(x)=28+10·sin(x/38)` 之后以 32% 坡下坠，脊线处就有 7 m 振幅横向起伏 → `horizon.py` 五列地平线跨度 **44 → 129 px**，画面里草地轮廓不再是一条等深直线
   - ⚠️ 仍未解决：**地平线还是一条硬边**（草地直接切到天空，没有空气渗染）。原因：全局线性雾 start=90 m，而脊线在 125 m 处，雾因子只有 ~2.5%；把雾调重就会把 110 m 的教堂一起洗掉
   - ✅ 已解决（02:35）：硬边的成因是「地面没有空气渗染」（`edge.py` 测出地平线单行跳变 86.3、地面距离提亮 -15.2）。竖直雾气帘不可行（透视把靠地平线的地面压成几十像素，帘幕只能涂窄条且长出地面的交线是新硬边），改由 `Mistbell/TerrainHaze` 按水平视距在 45→135 m 上 lerp 到天空色 → 跳变 4.1、提亮 +21.5
13. ~~**地平雾气帘 `haze.png`**~~ ✅ 改为**地形距离雾**并已完成（2026-11-02 02:35）：放弃「竖直雾气帘」——相机贴地平视 + 4° 坡，靠地平线的地面在屏幕上被透视压到几十像素，帘幕平面只能涂到薄薄一条，而且帘幕从地面「长出来」的交线本身就是新的硬边。改为 `Mistbell/TerrainHaze` 着色器（光照后在**水平视距** 45→135 m 上 lerp 到天空色 `#A7B7CB`），`edge.py` 量得地平线单行最大跳变 **86.3 → 4.1**、地面距离提亮 **-15.2 → +21.5**，近景草地 `(68,90,28)` 一字未动
6. ~~**带状移轴 RendererFeature**~~ ✅ 已完成（2026-11-02 02:52）：用 URP 17 内置 `FullScreenPassRendererFeature` + `Mistbell/TiltShift`（单 Pass 24 抽样黄金角螺旋、`_FocusBand 0.26 / _Falloff 0.24 / _MaxRadius 10 px`），Feature 存成独立资产 `Assets/Settings/TiltShiftFeature.asset`。`sharp.py` 三带高频能量：mid **完全不变**（4.58 / 15.76 / 12.58），top **-59%**，bottom **-58%~-75%**。`art_close` 目视已出现《歧路旅人》式微缩景深
   - ⚠️ 但 `art_runtime` 里肉眼读不出：前景草地本来就没有细节可虚化（hf 7.76 是纯纹理噪声）。**要等前景有可辨认物件（灌木/石头/栅栏/路缘）之后这一层的视觉收益才成立** → 并入下方「前景细节」
14. **石材重做** ✅ 主体完成（2026-11-02 03:19，四轮）：**主视角的「迷彩拼布」消失**，立面读成统一的暖砂岩石墙、块可数（约 21 px）。
   - v1：`sat 0.415→0.242`、`R-B +57.7→+31.5`、`blockW std/mean 0.417`（丢细颗粒、5 皮不整除 256）
   - v2：4 皮 / 拆统一描边 / 灰缝每条独立 / 法线 256 直出 / 暖度 `R-B +39.9`、`seam` 修复
   - v3：**块尺度才是病根** —— `patchwork.py` 证明颜色方差与参考持平（邻块明度差 5.4% vs 参考 10.8–14.2%），真因是块太小（0.53×0.6 m → 主视角 12 px）；`m_Scale` 1/2.4→1/4.8
   - v4：贴图块间明度抖动 ±0.09→±0.03、色相 ±6→±2.5°、sat→0.12 → 贴图 `blockSpread 74.3%→29.2%`；tiling 从 `.mat`（**被 gitignore 忽略、不可复现**）挪进 `SpikeScene.cs`；移轴 `_MaxRadius 10→12`
   - ⚠️ 遗留（近读仍像「现代砌块」）：① 块面太平滑、无接触阴影 → **SSAO（队列 11）**；② 灰缝部分偏绿、亮度不均；③ `Lstd 44.5→28.1` 与 `coarseStd 27→9.6` 偏低，需要块内起伏/凹陷把细节补回来
15. **前景细节物件**：草地是一整片平涂绿色，缺《歧路旅人》那种「近景点缀」——需要灌木丛/碎石/路边栅栏/路缘石各若干（可用体块 + 像素材质快速做），既补画面层次，也让移轴的虚化有东西可虚。**主视角最致命的一项**（HD-2D 观感一半来自近景细节 + 移轴虚化）
16. **石缝锐度 + 砂岩颗粒**（2026-11-02 04:30 新增，次优先）：倒角现在是 3 texel 阶梯，在 art_close 尺度（约 143 px/m）摊成 8 px 宽灰晕 → 砖缝读成「软灰带」而不是「清晰凹槽 + 接触阴影」；块面像石膏/泡沫，砂岩粒感弱。修法：`make_stone` 倒角收到 1–2 texel（高度 0.35→0.6）、缝宽 1–3 px、`blotch` 振幅 88→50（低频斑驳现在把颗粒盖住了），并考虑 256→512（106 texel/m；4 皮 × 512 要整除，`seam.py` 必须复测）
17. **屋顶色病态**（2026-11-02 04:30 新增）：主视角屋顶是番茄红 + 黑噪点，要压到「深石板青灰」并去掉黑点；与 16 同批（都是 `make_textures.py`）

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
| 01:02 | 复核上轮「fov 28」缺陷 | 读 `logs/20_runtime_003140.log` | **假警报**：成功那趟 `camera=fov 35`、pose[0] fov=35.0；28 只出现在 00:30 那次**没重跑 SpikeScene.Run** 的旧场景截图里。结论仍是「build 前必须重生成场景」，不必找第二台相机 |
| 01:05 | 派发 Codex 第 6 轮（队列 9） | `codex.sh` 读 `spike/brief-fix-terraces.md`（后台跑，日志 `logs/70_codex_terraces.log`） | 根因写成明文交代：台地平涂不是缺材质，是 `smart_project` 的 0..1 UV 与「1 UV=1 m」材质约定冲突（34 m 面只铺 1 块贴图 → mip 成纯色） |
| 01:08 | 验收 Codex 第 6 轮（人工独立复跑） | `blender -b --python spike/tools/build_church.py -- --verify` / `frame.py` ×3 / `vision_analyze` ×3 | **通过**：`[VERIFY] Terrace UV spans u=33.979 v=23.979 metres`、`UVSCALE OK`、VERIFY=0、tris=52384（几何未动）；`60_scene.log` church heightPct 49.8 / porch centerX 924.6 / pitch 11.8 与上轮一致；运行时 `pose[0] fov=35.0`。削顶 0.00% / 0.23% / 0.10%（原 1.41 / 23.20 / 48.05）；`art_ground` 1241 KB（原 50 KB）`ground_near std=15.36`。目视：台地正面石砌块缝清晰、草地有像素颗粒、石板路与台地转角同框。**判断**：平涂消失，近景终于读得出「石头砌的墙」；但门洞/窗洞仍是平涂棕、台阶读不出级差、远山依旧纸片 → 队列 10 + 4 |
| 01:13 | 派发 Codex 第 7 轮（队列 4 第 1 轮） | `codex.sh` 读 `spike/brief-fog-ridges.md`（日志 `logs/80_codex_ridges.log`） | 产出 `RidgeFog.shader` + `mist.png`(1024×256) + 两条雾带；编译 0 error、双平台 Succeeded |
| 01:16 | 人工读图：洗白 | `capture_runtime.sh` + `vision_analyze` | **判定不合格**：远山整体变白纸片、三层分不开。两处参数错：雪线 110/250 m 把 90% 山体判成雪；`fogEnd=650` 让 700 m 那层 100% 融进雾。另**我的简报写错了雾带方向**（相机在 z=-100 朝 +z，雾带该在 +170/+330，我写了 -170/-330），Codex 照字面执行并主动报告冲突 |
| 01:24 | Codex 第 8 轮修正 + 验收 | `codex.sh` 读 `spike/brief-fog-ridges-fix.md`（日志 `logs/82_codex_ridges_fix.log`）→ 我复跑 `frame.py` + 读图 | 雾 90/1500、fogColor=#A7B7CB（= 天空色）、三层 #44546A→#A9B8C6、雾带 z=170/330。`frame church 49.8 / pitch 11.8 / porch 924.6` 未动；削顶 0.00/0.23/0.10%；sky (167,183,203)。**目视**：三层山可数、都深于天空、白描边消失、教堂没被雾盖住；**但雪几乎看不见**（第 2 层顶点世界高度只 185 m，压不到 300 m 雪线）、**雾带在画面里读不出来**。**新发现主因**：坡地 200×260 的远端在 z=+100 断成一条**笔直的天地交界线**，远山浮在它后面 → 「桌面模型」感来自地形收边，不是山 → 队列 12 |
| 01:29 | 新工具 `tools/horizon.py` | 逐列找第一条草绿行，量「地平线是否等深」 | 现基线 span=44 px（5 列 x=192/480/960/1440/1728）；判据 <15 px = 一刀切地平线 |
| 01:31 | Codex 第 9 轮（地形网格 v1） | `codex.sh` 读 `spike/brief-terrain-edge.md` | 坡板换成 23103 顶点网格、MeshCollider；`pilgrim y=terrain y=-8.5286`。**画面没变**：我给的剖面 `0.0012·(z-60)^1.9` 太缓，z=60 处那道脊比后面都高，等于新立了一道直边挡住谷地。Codex 主动报「验收未通过」 |
| 01:38 | Codex 第 10 轮（脊线下坠 v2） | `codex.sh` 读 `spike/brief-terrain-ridge.md` | 剖面改「z≤25 沿用 g(z)、之后 32% 下坠」；仍 span=37 px —— 因为起伏项 `smoothstep(25,90,z)` 在脊线处振幅=0，可见轮廓依旧等深。**教训：轮廓只看脊线那一圈，起伏必须作用在脊线上** |
| 01:45 | Codex 第 11 轮（脊线起伏 + 雾带压脊） | `codex.sh` 读 `spike/brief-terrain-crest.md` | `crestZ(x)=28+10·sin(x/38)`、脊线处 7 m 振幅 → `horizon.py` span **129 px**（44→38→129）；`frame church 49.8 / pitch 11.8 / porch 924.6`、`pilgrim y=terrain y` 全未动；削顶 0.00/0.22/0.11%。**目视**：草地轮廓终于起伏（右侧成小丘），但**地平线仍是硬边**、噪音雾带在 125 m 处读不出来（全局雾因子仅 2.5%）。定性结论：要用竖直渐变雾气帘，不是噪声雾带 → 队列 13 |
| 02:18 | 新工具 `tools/edge.py` | 逐列求地平线单行亮度跳变 + 地面距离提亮 | 基线：mean jump **86.3**（>30 判硬边）、distance-lift **-15.2**（越远越暗，说明完全没有空气渗染）。这个工具替掉了「肉眼说有硬边」这种不可复现判断 |
| 02:20 | 改方案：雾气帘 → 距离雾 | 我重写队列 13，新简报 `spike/brief-terrain-haze.md` | 否掉帘幕的理由：贴地平视 + 4° 坡，靠地平线的地面在屏幕上只有几十像素，帘幕平面只能涂窄条；且帘幕从地面「长出来」的交线本身是新硬边。距离雾在 45→135 m 累积、由透视自然压缩成几十像素渐变 |
| 02:24 | Codex 第 12 轮（地形距离雾） | `codex.sh` 读 `spike/brief-terrain-haze.md`（日志 `logs/96_codex_haze.log`） | 新增 `Assets/Art/Shaders/TerrainHaze.shader`（主光 Lambert + 主光阴影 + SH 环境光，光照后按水平视距 lerp 到天空色 45/135 m，power 1.4、strength 0.92）+ `SpikeScene.MakeGroundHaze()`（只换地形材质，几何/相机/雾/远山未动） |
| 02:33 | 人工验收（我独立复跑全部命令） | `edge.py` / `sharp.py` / `frame.py` / `horizon.py` / `vision_analyze` | **通过**：地平线单行跳变 **86.3 → 4.1**（6 列全部 3.9–4.3，判软过渡）、地面距离提亮 **-15.2 → +21.5**；近景草地 `ground_near` 仍是 (68,90,28)（偏差 0）；削顶 0.00/0.22/0.11%；church heightPct 49.8 未动；双平台 Succeeded（macOS 120 MB / Windows 102 MB）、error CS 0。**目视**：左侧与右侧小丘都变成雾中渐隐，「草地一刀切天空」消失；代价是远山山脚也被洗淡（可接受，符合空气透视）。`horizon.py` span 44 是**度量失效**不是几何变了——雾把远处的绿压没了，绿色阈值检测点前移；几何证据是 `terrain mesh verts=23103` 未变 |
| 02:38 | 拆解遗留：edge.py 采到教堂纹理边 | `probe_col.py` 打印 x=480/960 逐行 RGB | 上轮 2 个大跳变（141.8/130.7）里 x=960 整列落在教堂石墙上、x=480 落在远山山脚——不是天地交界。改为默认列避开 600–1300 中央区、跳变窗口收窄到 [y0-3, y0+16]，复测 mean jump **4.1**。**教训**：验收脚本的采样列必须避开主体，否则量的是石头的纹理边 |
| 02:40 | 新工具 `tools/sharp.py` + 派发队列 6 | 三带高频能量（邻像素亮度差均值）；简报 `spike/brief-tiltshift.md` | 基线 top/mid/bottom：runtime 0.00/4.58/7.76、close 13.87/15.76/12.03、ground 8.42/12.58/4.98。判据：middle 变化 ≤12%、top/mid 与 bottom/mid 降 ≥30% |
| 02:44 | Codex 第 13 轮（带状移轴） | `codex.sh` 读 `spike/brief-tiltshift.md`（日志 `logs/97_codex_tilt.log`） | 新增 `TiltShift.shader`（24 抽样黄金角、`smoothstep(_FocusBand,_FocusBand+_Falloff,d)` 带形遮罩）+ `SpikeURP.TiltShift()`（材质 `M_TiltShift.mat`、Feature 独立资产 `TiltShiftFeature.asset` 挂进 `MistbellRenderer.asset`）。**关键点**：Feature 必须是独立 `.asset`，不能用子资产（重蹈 VolumeProfile 不进包的坑） |
| 02:52 | 人工验收（我独立复跑） | `sharp.py` / `frame.py` / `edge.py` / 构建日志 / `vision_analyze` ×2 | **通过**：hf bottom 7.76→**1.95**（-75%）、close top 13.87→**5.67**（-59%）、bottom 12.03→**5.01**（-58%）、ground 8.42→3.62 / 4.98→2.73，**middle 三张全部一个数字都没变**（4.58/15.76/12.58）——证明模糊只发生在带外、主体一点没糊；`frame sky (167,183,203)` 与 `ground_near (67,90,28)` 未漂、削顶 0.00/0.15/0.07%；`edge.py mean jump 4.1` 未破；church heightPct 49.8；双平台 Succeeded（120 MB / 102 MB）、error CS 0。**目视**：`art_close` 已出现微缩模型景深（顶墙上缘与底部草地虚、门廊与台阶锐），`art_runtime` 看不出——前景草地本来就没细节可虚。**判断：像 HD-2D 了，但离参考还差两块——石材是规则米色砖网格、前景空无一物** → 队列 14 / 15 |
| 03:02 | 新工具 `texstat.py` / `region.py` + 石材基线 | `texstat.py stone_ashlar.png`、`region.py art_close.png 1400,200,1750,420` | 关键发现：**贴图本身是中性灰**（meanRGB 126,123,116 sat=0.090），墙面却是米色（sat=0.415 R-B=+57.7）→ 偏黄全来自材质 `_BaseColor` 硬编码 `(0.647,0.549,0.425)`；块规整度用 `coarseStd=17.61` 量化。简报 `spike/brief-stone.md` 写好（11 条门槛），上一轮不派发以免与调度撞车 |
| 03:05 | Codex 第 14 轮（石材 v1） | `codex.sh` 读 `spike/brief-stone.md`（日志 `logs/100_codex_stone.log`） | 贴图 128→256、5 皮随机块宽、灰缝 2–4 px、流痕/积尘、材质 `_BaseColor` 米色→灰褐。我独立复跑：`region` sat 0.415→**0.242/0.243**、R-B +57.7→**+31.5/+28.1**、Lstd 39.4/38.6、`texstat` coarseStd 24.55 / edge% 5.0 / sat 0.126、blockW std/mean 0.417；`edge.py 4.1` 与 `church heightPct 49.8` 未破。**两项未过**：`sharp art_close middle 15.76→11.05`（-30%，细颗粒丢了）；`make_textures` 打印的 `seam 12.56/19.52` 是**度量失效**（seam_score 用全局 SZ=128 索引，256 贴图量的是左上象限的内部边） |
| 03:10 | 新工具 `tools/seam.py` | 按 PNG **真实尺寸**量环绕边 vs 内部相邻像素差 | 发现两处真缺陷：**stone row ratio 3.11**（5 皮 × 51.2 px 不整除，末行落在块体中间）、**roof_slate col/row 9.39/13.24**（屋顶每 2.4 m 一条可见横缝，早已存在但没人量过） |
| 03:12 | Codex 第 15 轮（石材 v2） | `codex.sh` 读 `spike/brief-stone2.md` | 4 皮（256 整除）、拆掉每块统一描边、灰缝每条独立 1–5 px、blotch ±72→±20、grain ±13→±22 + 细颗粒、法线改 256 直出、stone `_BaseColor` (0.675,0.63,0.575)。我独立复跑 15 条：**全过** —— R-B **+39.9/+38.3**、sat 0.293/0.286、Lstd 46.6/45.6、`sharp middle 13.54`、`seam` stone **0.75/1.23** / roof **0.40/0.17** / grass 0.89/1.05、clipped 0.00/0.35/0.79%、heightPct 49.8、双平台 Succeeded / error CS 0 |
| 03:20 | 目视判定 + 新工具 `tools/patchwork.py` | `vision_analyze` ×4 + 概念图/目标图对照 | **v2 指标全过但主视角更难看**：立面成了「白块+深灰块」迷彩拼布。改量：邻块明度差中位数 **5.4%**（目标参考 target-front 10.8–14.2%）、块间饱和度离散 **26.2%**（参考 26.2–28.2%）→ **颜色方差与参考持平，不是病根**。真因是**块尺度**：一张贴图 2.4 m / 4 皮 → 块 0.53×0.6 m，主视角 19.4 px/m → 一块石头只有 **12 px**、立面 60+ 块 = 细碎噪声；参考里石块约占建筑宽度 9–10%（2.4–2.8 m）。简报 `spike/brief-stone3.md` 写好（只改 `M_Stone.mat` 两处 m_Scale：1/2.4 → 1/4.8，块变 1.05×1.2 m、主视角约 21 px） |
| 03:16 | Codex 第 16 轮（石材 v3：块尺度） | `codex.sh` 读 `spike/brief-stone3.md` | `m_Scale` 1/2.4→1/4.8，主视角块 12→**21 px**，块终于可数；`region R-B +46.6/+39.8`。副作用：`sharp bottom/mid 0.34→0.49`、`art_ground clipped→1.18%`。**副产物发现**：`M_Stone.mat` 被 `.gitignore`（`spike/project/Assets/*`）忽略 → 改 `.mat` 等于改了不可复现的隐藏状态 |
| 03:18 | Codex 第 17 轮（石材 v4：块间色差） | `codex.sh` 读 `spike/brief-stone4.md` | 贴图块间明度抖动 ±0.09→±0.03、色相 ±6→±2.5°、块内 sat 上限 0.18→0.12；**石材 tiling 挪进 `SpikeScene.cs`**（`[SPIKE] stone tiling=(0.2083,0.2083)`，新克隆可复现）；移轴 `_MaxRadius 10→12`。我独立复跑：贴图 `blockSpread 74.3→**29.2%**`、`neighborDiff 14.3→7.4%`；`region R-B +46.9/+41.6`、`sat 0.295/0.291`、`clipped 三张全 0.00%`、`sharp bottom/mid 0.45`（恢复）、`heightPct 49.8`、双平台 Succeeded / error CS 0。**代价**：`Lstd 44.5→28.1`（<32）、`coarseStd 27.06→9.60` —— 近读变「干净的水泥砌块」 |
| 03:19 | 目视判定（石材 v3+v4） | `vision_analyze` on `art_runtime` / `art_close` | **主视角拼布消失**：立面读成统一的暖砂岩石墙，块可数、色温接近目标参考 `target-front-hd2d`；这是石材第一次「像一面墙」而不是「贴纸网格」。**近读仍像现代砌块**：块面平滑、无接触阴影、部分灰缝偏绿。剩余三块：屋顶番茄红+黑噪点、远山纸片、前景空无一物 → 队列 11 / 15 |
| 03:46 | 新尺子：`region.py` 加 `p5/p50/tail` | 追加字段（不改原有门槛） | 基线（石材 v4 + 无 AO）：墙 `1400,200` p5=77.1 p50=145.9 **tail=0.529**；台阶 `700,600` 0.517；台地平墙 `1300,760` 0.557。用来判「凹陷有没有真的压暗」，而不是整体变暗/变亮 |
| 03:48 | 派发 Codex 第 18 轮（SSAO + 块面起伏） | `codex.sh` 读 `spike/brief-ssao.md`（日志 `logs/118_codex_ssao.log`） | `SpikeURP.SSAO()` 建独立 `SSAOFeature.asset`（**Source=Depth**：`TerrainHaze.shader` 只有 Forward/ShadowCaster/DepthOnly，没有 DepthNormals pass）；`make_stone` 加块面倒角+块心微凹、灰缝改**三通道共用偏移**（消掉粉/绿彩线）；`_BumpScale=1.8` 挪进 `SpikeScene.cs` |
| 04:05 | 人工验收（8 条全复跑） | `region`/`patchwork`/`texstat`/`seam`/`sharp`/`frame`/构建日志 | **全过**：墙 tail **0.529→0.489**、三块 p50 **145.9/141.7/160.0**（0.00%/−2.75%/+3.76%）、clipped 0/0.07/0%；`blockSpread 21.4%`、`neighborDiff 7.3%`；`coarseStd 9.60→16.39`、`sat 0.104`、`edge% 4.9`；`seam 0.64/1.30`；`sharp middle 10.85`；`heightPct 49.8 / pitch 11.8` 未动；双平台 Succeeded、error CS 0；`ssao feature=… intensity=0.75 radius=0.6 source=Depth`、`features=2` |
| 04:12 | **差分验证**：SSAO 到底贡献了多少 | `m_Active:0` → `SpikeBuild.Run` → `capture_runtime.sh` → `region.py`；证据 `Shots/ssao_off_close.png` | 同一份贴图下 SSAO(0.75/0.15/0.6) 只让墙面 tail **0.496→0.489（-1.4%）**、台阶 0.462→0.459、**平墙 0.583 一动不动**。结论：**SSAO 只能压暗几何凹角，压不动贴图砖缝**；且 URP 默认 `AfterOpaque=false` 时 AO 主要作用在间接光上，正立面被太阳直打 → 强度太低等于没开 |
| 04:26 | SSAO 调参（写进代码，可复现） | `SpikeURP.cs`：`Intensity 0.75→2.2`、`DirectLightingStrength 0.15→0.85`、`Radius 0.6→0.5`；重跑 `SSAO`→`Build`→`capture` | 墙面 p50 **147.7→140.3（-5.0%）**、Lstd 31.8→33.5、tail 0.496→0.477；台阶/门廊 p50 **151.9→100.8（-34%）**（门廊凹角重，台阶级差终于读出来）；平墙 0.583 仍不动。`frame`：sky (167,183,203)、ground_near (67,90,28)、clipped 0.00% **一字未动**；双平台 Succeeded / error CS 0 |
| 04:30 | 目视判定（SSAO + 块面起伏） | `vision_analyze` on `art_close` / `art_ground` / `art_runtime` | **通过**：灰缝粉/绿彩线消失（改中性后肉眼确认）；块面有倒角与微凹、拱洞与门廊有接触阴影、台阶读得出 5 级、石板路与草地交界有接地感；`art_ground` 无 AO 脏斑。**新问题**：① 倒角 3 texel 在 art_close 摊成 8 px 灰晕，砖缝变成「软灰带」而非「清晰凹槽」；② 块面像石膏/泡沫，砂岩粒感弱；③ 主视角屋顶番茄红+黑噪点、前景仍空 → 队列 16 / 17 / 15 |
