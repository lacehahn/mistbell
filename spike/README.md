# Spike：Blender → Unity → 目标包 管线验证（3 天）

> 目的：在写任何游戏逻辑之前，用一个**可抛弃**的最小工程证明管线成立。
> 依据：`docs/04-tech-stack.md` 的「D01 落地前的 3 天管线 spike」与 `docs/01-art-direction.md` 的「D03 A/B 验证方案」。
> 状态：等待 Unity 6.3 LTS 安装完成（今日）。**本工程不入主库**，只提交简报、规格与截图证据。

## 进入条件

- [ ] Unity Hub 已登录，6.3 LTS（6000.3.27f1）已安装，含 macOS IL2CPP + Windows Mono 模块
- [ ] Blender 已安装并记录版本（当前 brew cask 为 5.2.3，需在可行性确认后写回 `docs/04-tech-stack.md`）
- [ ] `unity -batchmode` 能在空工程上完成一次编译并落日志

## 三天任务

| 天 | 验证项 | 通过标准 | 不通时的退路 |
| --- | --- | --- | --- |
| D1 | Blender 教堂外壳（≈2 万面简化体）→ FBX → URP：单位／朝向／缩放、材质映射到 URP Lit、法线与 UV | Unity 内比例与 Blender 一致（1 m = 1 unit）；石材无黑面／翻面；同一套材质设置在 6 小时内可第二次复现 | 改用 glTF 导入包；或在 Blender 内烘顶点色／顶点 AO 兜底 |
| D2 | 光照烘焙（Lightmap）＋混合遮挡：Billboard 像素角色与 3D 建筑的前后关系 | Generate Lightmap UVs 后烘焙无接缝、无漏光；角色在柱后／门内被正确遮挡，不出现整片贴片穿墙 | 改实时阴影＋雾层伪装纵深；或按 D03 改正交投影降低遮挡压力 |
| D3 | 两个自定义 Pass：带状移轴模糊、局部光雾缓冲（仅覆盖揭示方向约 80 m） | 1080p 下两者叠加仍 ≥ 60 fps（本机 M6）；雾边无硬边、不污染 UI／文字层 | 降级为全屏 tilt-shift ＋ 3 层雾片；或把雾推后到 M3 再试 |

## 每日产出物（必须落盘，可追溯）

```
spike/
├── project/                  # Unity 工程（git 忽略，可抛弃）
├── blend/gergeti_shell.blend # 教堂简化体源文件
├── export/gergeti_shell.fbx  # 导出物
├── shots/                    # 截图证据，命名见下
└── logs/                     # batchmode 编译与性能日志
```

截图命名：`d3_{A|B}_{shot}_{plain|post}.png`（D03：6 机位 × 2 后期状态 = 12 张）
性能记录：`perf_{date}_{scene}.csv`，含平均帧与 1% low，冷启动单独一行。

## 退出判定

- **D1+D2+D3 全过** → 按 `docs/05-mvp.md` 的 M1 开工，spike 工程归档删除，只留证据。
- **D1 不过** → 停止，重估导入管线（glTF）与 D01，不进入批量建模。
- **D2 不过** → 先做 D03 的 A/B 截图对比，用相机方案绕过遮挡问题，再回到 D2。
- **D3 不过** → 按 `docs/04-tech-stack.md` 的降级路径处理，把雾推后到 M3，**不切引擎**。

## 预算

3 天 × 2 人 = 48 人时（不占 M1 的 80 人时）。以 AI 为主要实现者时的实际含义：3 个会话日，每天含 1 次人工审美／验收。
