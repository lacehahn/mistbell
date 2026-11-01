# PROGRESS — Mistbell 自主推进日志

> 规则：每完成一步就在「日志」里追加一行（时间 | 步骤 | 命令/产物 | 结果）。任何新会话先读本文件末尾 3 条 + `git log --oneline -5` 再动手。
> Codex 限流时：在日志记「Codex 限额耗尽，等待重置」，本步跳过，下一次调度再试；**不要**因此中断其它不依赖 Codex 的工作。

## 当前状态（最后更新：2026-11-02 22:30）

- 环境：Unity Hub 3.22.5 / Unity 6000.3.27f2 / Windows Mono 模块 / macOS 目标内置 / Blender 5.2.3 LTS / Unity Personal 已激活
- 闭环已验证：
  - `-batchmode -executeMethod SpikeBuild.Run` → macOS 102 MB、Windows 86 MB，均 `Succeeded / 0 errors`
  - `-batchmode -executeMethod SpikeCapture.Run` → 渲染 PNG 落盘成功，AI 可直接读图判断
- 已知问题：标尺屏高实测 149.1 px（D03 期望 ~172 px）；怀疑标尺被立方体遮挡或视轴距离与期望不符，待用无遮挡场景复核
- 美术目标（用户 2026-11-02 指示）：**尽可能实现类似《歧路旅人》的 HD-2D 风格**

## 待办队列（按顺序，未完成的在前）

1. **构图修正**：教堂 14 m 高，当前机位把塔顶切掉了。改为 target=(0,6,0)、distance=45、pitch=20，并把角色放在相机前方约 10 m 处确保入画
2. **曝光与光照**：当前整体偏暗。sun 强度 1.6→2.2、ambient 0.30→0.45、postExposure +0.2→+0.4；bloom threshold 1.0→0.9
3. **程序化像素贴图**（HD-2D 观感的关键，现在全是纯色块）：用 `tools/make_textures.py` 生成草地/石砌/屋顶瓦片贴图（64×64 可平铺），导入设 Point + 无压缩 + Repeat
4. **雾的分层**：3 层半透明雾片（近/中/远，不同密度与色温）+ 现有线性雾，制造纵深
5. **角色动画与朝向**：3 帧循环 + 按相机方位切换 down/up/left/right
6. **带状移轴 RendererFeature**：URP 的 Bokeh DoF 是体积式，不是《歧路旅人》那种上下带状遮罩；需自定义 Pass（排除角色与 UI）
7. **D03 的 12 张 A/B 截图**（低透视 vs 正交 × 6 机位 × 关/开后期）
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
