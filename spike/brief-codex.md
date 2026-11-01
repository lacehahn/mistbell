# 给 Codex 的 spike 任务书（每天一份，喂给它时只贴对应那一段）

## 上下文纪律（每天都必须遵守，直接决定 token 成本）

你已经因为这个项目以外的原因烧掉 3.2 亿 token，其中 65% 花在"通读整个仓库"上。这次不许：

- **只读下面列出的文件**，不要 `find`／`rg`／`ls -R` 全仓扫描，不要读 `docs/` 里没列出的文档。
- 每次回答前先说明"我要读哪几个文件"，读完再动手。
- 不要在任何回复里回贴整份文件内容；改了什么就说改了哪几个函数／几行。
- 遇到不确定的 Unity API，先查官方文档（`docs.unity3d.com/6000.3/`），**不要凭记忆写**；写错版本 API 比慢更贵。
- 每天结束时输出固定格式（见文末），不要写散文。

## 背景（一句话）

2 人（其中 1 人为 AI）在做 HD-2D 探索 RPG，首个地点是格鲁吉亚 Gergeti 圣三一教堂。当前在 Unity 6.3 LTS（6000.3.27f1）+ URP 上做 **3 天管线 spike**，验证 Blender → Unity → Windows/Mac 包这条链路。**spike 结束后工程会被丢弃**，所以优先验证可行性，不追求代码整洁。

必读（仅这些）：
- `spike/README.md`（目标、产出物、判定）
- `docs/01-art-direction.md` 的「## 2. 相机规范」与「### D03 A/B 验证方案」两节
- `docs/04-tech-stack.md` 的「### D01 落地前的 3 天管线 spike」一节

## D1：Blender → FBX → URP

输入：`spike/blend/gergeti_shell.blend`（若不存在，先用 `bpy` 脚本建一个简化教堂外壳：主体方体 + 钟楼 + 门廊 + 拱门开口，目标 ≈ 2 万三角面，1 m = 1 unit，Z 轴向上）。

任务：
1. 用 `bpy` 脚本导出 `spike/export/gergeti_shell.fbx`（`FBX_SCALE_ALL`，应用变换，含 UV）。
2. 在 Unity 里导入，写一个 Editor 脚本 `Tools/Spike/ImportAndReport.cs`：把网格的 bounds、三角面数、UV 通道数、法线朝向异常面数打印到日志。
3. 建 3 个 URP Lit 材质（石材／屋顶／木门），用 `Tools/Spike/MaterialMap.cs` 自动按材质名映射，避免手点 Inspector。
4. 跑 `Unity -batchmode -quit -executeMethod Spike.ImportAndReport.Run`，把日志写进 `spike/logs/d1_import.log`。

验收（必须在报告里逐条回答是／否）：
- [ ] Unity 内 bounds 与 Blender 内一致（误差 < 0.5%）
- [ ] 三角面数与 Blender 内一致
- [ ] 无黑面／翻面（法线异常面数 = 0）
- [ ] 材质映射后无粉色（missing shader）材质
- [ ] 同上流程第二次执行能复现（记录耗时）

## D2：光照烘焙 + Billboard 遮挡

任务：
1. `Assets/Spike/Scene.unity` 用脚本生成：15° 斜坡走道（长 60 m）、2 根石柱、1 个门洞、坡顶放置教堂外壳。
2. 导入一张 64×80 的占位角色图集（4 方向 × 1 帧即可），用 Y 轴锁定 Billboard shader，脚底对齐地面。
3. 开 Generate Lightmap UVs + 烘焙（`LightingSettings` 用脚本配置，分辨率 20 texels/m）。
4. 角色从起点走到坡顶，沿路经过柱后与门内，导出 6 张截图（对应 `docs/01-art-direction.md` 的 6 机位）到 `spike/shots/`。

验收：
- [ ] 烘焙无接缝、无漏光（门内与柱后各 1 张特写）
- [ ] 角色在柱后／门内被正确遮挡，无整片贴片穿墙
- [ ] 8 个采样点遮挡边界误差 ≤ 1 px（截图量取，记录数值）

## D3：两个自定义 Pass + 性能

任务：
1. URP RendererFeature：**带状移轴模糊**（屏幕自顶 25% 与底部 15% 渐进，上缘最大 4 px、下缘 2 px；排除角色与 UI 层）。
2. URP RendererFeature：**局部光雾缓冲**（1/4 分辨率，仅覆盖揭示方向约 80 m，最多 16 次密度采样，1 盏方向光）。
3. 出 A／B 两套相机（透视 FOV 28°／d=22 m，与正交 11 m）各 6 张、关后期／开后期各一张 = 12 张，命名 `d3_{A|B}_{shot}_{plain|post}.png`。
4. 性能采样：揭示镜头 60 秒，输出平均帧、1% low、冷启动单列。

验收：
- [ ] 1080p 下两个 Pass 叠加 ≥ 60 fps
- [ ] 雾边无硬边、不污染文字层（32 px 中文叠图检查）
- [ ] 12 张截图齐全、命名正确
- [ ] 降级路径可用：关掉局部光雾后仍 ≥ 60 fps

## 禁止事项

- 不要写任何游戏逻辑（无移动控制、无交互系统、无存档）——D2 的角色移动用最简单的 `transform.Translate` 即可。
- 不要引入第三方包，除非现有 URP 无法完成；引入需先说明理由与体积。
- 不要 git commit（由 Hermes 在人工验收后统一提交）。
- 不要在 `docs/` 里新建文件。

## 每日报告格式（固定，不要加代码块）

```
DAY: D1
改动: <文件>:<要点>; <文件>:<要点>
命令: <执行的命令>
证据: <日志路径> <截图路径>
验收: 项1=是/否(数值) 项2=是/否 项3=...
阻塞: 无 / <一句话>
建议下一步: <一句话>
```
