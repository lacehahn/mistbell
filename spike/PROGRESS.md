# PROGRESS — Mistbell 自主推进日志

> 规则：每完成一步就在「日志」里追加一行（时间 | 步骤 | 命令/产物 | 结果）。任何新会话先读本文件末尾 3 条 + `git log --oneline -5` 再动手。
> Codex 限流时：在日志记「Codex 限额耗尽，等待重置」，本步跳过，下一次调度再试；**不要**因此中断其它不依赖 Codex 的工作。

## 当前状态（最后更新：2026-11-01 23:25）

- 环境：Unity Hub 3.22.5 / Unity 6000.3.27f2 / Windows Mono 模块 / macOS 目标内置 / Blender 5.2.3 LTS / Unity Personal 已激活
- 闭环已验证：
  - `-batchmode -executeMethod SpikeBuild.Run` → macOS 113.7 MB、Windows 95.7 MB，均 `Succeeded / 0 errors`
  - `-batchmode -executeMethod SpikeScene.Run` → 建场景 + 落编辑态参考图
  - `caffeinate -dimu -t 120 bash tools/capture_runtime.sh` → 播放器按 `ShotRig` 机位表逐张出图（多机位一次出包）
- **⏰ 运行时截图必须先 caffeinate**：显示器休眠时 Unity 播放器不会推进主循环（日志停在 `UnloadTime`，进程 0% CPU），截图会「静默失败」而不是报错。cron 夜间跑尤其容易踩。
- 场景几何约定（后续沿用）：坡地 4°、`GroundY(z)=tan4°·z`（z=0 处 y=0）；教堂整体抬高 `ChurchBaseY=0.6`；**门廊朝 -z（来路侧）**，玩家自下坡上来看到的是正立面。
- 机位表在场景里的 `ShotRig`（`Assets/Scripts/ShotRig.cs`）：`art_runtime`（reveal，pitch 16/d 50/yaw 40）与 `art_close`（pitch 14/d 14/yaw 12）。
- 已知问题：像素角色在画面中比预期小——图集格子 64×80 里角色本体只占约 45% 高度，实际屏高约为 quad 的 0.45 倍（放队列 5 一起处理）
- 美术目标（用户 2026-11-02 指示）：**尽可能实现类似《歧路旅人》的 HD-2D 风格**

## 待办队列（按顺序，未完成的在前）

1. ~~**构图修正**~~ ✅ 已完成（2026-11-01 23:20）：塔顶 y=95px 余量、基座 y=859px、朝圣者 y=921px 入画
2. ~~**曝光与光照**~~ ✅ 已完成（2026-11-01 23:21）：sun 1.5 / ambient 0.45 / postExposure 0.4 / bloom 0.9 / sun yaw +25（先试 2.2 过曝，回调）
3. **程序化像素贴图**（HD-2D 观感的关键，现在全是纯色块）：用 `tools/make_textures.py` 生成草地/石砌/屋顶瓦片贴图（64×64 可平铺），导入设 Point + 无压缩 + Repeat
4. **雾的分层**：3 层半透明雾片（近/中/远，不同密度与色温）+ 现有线性雾，制造纵深
5. **角色动画与朝向**：3 帧循环 + 按相机方位切换 down/up/left/right；顺带修「角色在图集里偏小」的问题
6. **带状移轴 RendererFeature**：URP 的 Bokeh DoF 是体积式，不是《歧路旅人》那种上下带状遮罩；需自定义 Pass（排除角色与 UI）
7. **D03 的 12 张 A/B 截图**（低透视 vs 正交 × 6 机位 × 关/开后期）——`ShotRig` + `-shots` 机制已就绪，直接扩机位表即可
8. **Codex D1**：用 Blender 建真正的教堂外壳替换体块（走 `spike/tools/codex.sh`）

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
