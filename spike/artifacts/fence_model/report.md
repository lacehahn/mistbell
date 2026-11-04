# 围栏固定模型验收（2026-11-04）

仅执行 [fence_model.md](../../tasks/fence_model.md)。完整命令 `bash spike/tools/accept_fence_model.sh` 已执行，exit=0，25/25 项通过；Unity 构建 Succeeded / 0 errors。没有修改 AGENTS.md、任务书、旧演示断言，没有安装依赖，没有提交。

## 实现与资产

- 局部幂等入口：`FenceModelBuild.Run`，不再运行全场景生成器。三个固定网格 `Assets/Settings/Fence_A.asset` / `Fence_B.asset` / `Fence_C.asset`，各 120 三角面，实际 Z 包围盒长 2.200000 m；原点在第一根柱脚中心（Z 外沿 -0.05…2.15 m）。柱 0.08×0.08×1.0 m，脚部 0.10 m；横杆截面 0.06×0.08 m，中心高 0.30/0.75 m；雪帽厚 0.035 m。B 的一根柱向路外倾 7°；C 缺下横杆，上横杆有不齐的两侧断口。闭合网格正体积检查通过，排除面朝向错误。
- 保留 3 个模型引用，沿路线实例重复，按实例矩阵合并到 `FM4_Batch`；没有每柱/每横杆的渲染对象。只保留每段模型引用和用于既有脚底回归的柱碰撞代理，围栏物理网格另由合并 MeshCollider 提供。坡度写入实例矩阵的 Y 分量，柱保持竖直，避免每段重做网格。
- 一张材质 `Assets/Settings/M_Fence_Model.mat`，一张贴图 `Assets/Art/Textures/Fence_Wood_1024.png`。复用现有手绘源 `art-prompts/staged/fence_wood.jpg`，由 Unity 无依赖导入缩放、三阶色板量化、左右接缝处理。色板 #382C22 / #4A3B2E / #5C4A38，少量 #7E796A 磨白/地衣。1024²、左右接缝不同像素 0。独立 `Mistbell/FenceModel` 为纯漫反射，无高光；限制木材的入射光强，保留纹理明度差。木纹方向沿各构件长轴，横杆每段一次。
- 两侧各一排，共 183 段，A/B/C=[146, 19, 18]；每 10 段随机打乱 8A+1B+1C，末尾不满一组。逐实例变换而不是逐构件变换，随机种子 6321。全围栏 21960 三角面。

## 布局与截图

```text
[FENCE_MESH] segments=183 tris=21960 batches=1 spacing=2.130466..2.343246 crossing=0 variants=3
```

`batches` 的口径是同材质合并后的 Renderer/网格提交组数，不是全帧 drawcall。播放器真实日志 `STATIC_REMAINING root=FM4_Root renderers=1 tris=21960` 与资产独立复算相符；URP 阴影等多 pass 成本计入下表全帧 drawcall。旧围栏 1 个 Renderer 全部移除，由 1 个替换。

间距由持久化世界坐标复算，仅统计同一直线区段相邻实例原点；折角单独作为断口，不将折角间距冒充正常步距。6 处转角柱脚几何净隙 0.414281–0.495572 m。偏航 -2.971344–2.875692°，高度抖动 -0.019877–0.019819 m。编辑器实际网格/路面三角形相交检查与独立 Python SAT 相交复算，均 crossing=0，不只检查柱中心。

- [cam_play_1080.png](cam_play_1080.png)：实际玩法机位，1920×1080。
- [fence_closeup.png](fence_closeup.png)：检查机位，1920×1080，距 C 段目标 2.644334 m；从受光侧看木纹、积雪帽和破口，关闭后期只影响该检查会话。两张图已目视复核：木纹及雪帽可辨，有高度/朝向变化和近处断栏，没有横穿路面。
- 近景射线/UV 导出 8423 个受光木面像素，由 PNG 独立计算三阶明度中位数 L*=31.28073 / 44.43766 / 52.44687，亮暗跨度大于 3。对应 `fence_closeup_pixels.csv` 和 `measurements.json`；同帧采样，不使用材质 RGB 冒充渲染结果。
- 截图新鲜度、1080p 尺寸、品红错误材质像素 0 均通过。

## 性能与三项演示

测量条件：Mac18,5 / Apple M6 / Metal / Unity 6000.3.27f2 Development Player，1920×1080 前台，vSync=0，targetFrameRate=-1；每点至少 180 帧且 3 秒预热，300 帧采样。1% low 为最慢 3 帧平均耗时的倒数，如实保留，不作为平均 200 FPS 门槛的替代。

| 测点 | 平均 FPS | 1% low FPS | drawcall |
|---|---:|---:|---:|
| z01 | 209.09 | 164.05 | 263 |
| terrace | 219.79 | 158.43 | 321 |
| interior | 215.52 | 158.22 | 256 |

任务要求的两个室外点平均 FPS 均 ≥200；台地 drawcall=321≤330。上轮任务记录台地 328 drawcall 供历史参考，本次没有另跑旧版本控制组。

- walk6：distance=15.395 m，ground_error_max=0，raycast_misses=0，bad=0。
- enter：INT_PASS 的 light/domeLight/ceilingSeen/domeSeen/visible/obstacles/walked 全 True，bad=0。
- reveal：原 gate=pass，bell_once=True / pulse_restored=True，bad=0。未修改演示断言或机位。
- 原玩法相机两段各 6 秒检查 bad=0，角色 158.478 px，FollowOffset=(0,2.10,-11.50)/LookHeight=1.20/FOV48 保留。

## 保护与复现

排除被替换的围栏对象后，对场景组件序列化数据、全部非围栏网格/材质、Transform 矩阵和 RenderSettings 做 SHA256；修改前后均为：

`8FF624E7646D2F351BA88C0673824D9148DB6C51F617B99278F4B5816033F9C4`

与首次替换前摘要相同。路面及海拔混合、室内、祭坛、人物、玩法/reveal 相机、雾与山保持一致。重复构建也通过同一摘要与布局检查。

原始证据：`spike/logs/fence_model_{build,camera,closeup,walk,interior,reveal,bench}.log`、`fence_model_bench.json`、`fence_model_acceptance.log`。详尽数字 [measurements.json](measurements.json)，持久化逐段与路面几何 [assets.json](assets.json)，可独立再次执行 `python3 spike/tools/verify_fence_model.py`；`--player-only` 仅用于已构建后的播放器复跑。

代码：`Assets/Editor/FenceModelBuild.cs`、`Assets/Scripts/FenceModelCapture.cs`、`Assets/Art/Shaders/FenceModel.shader`、`tools/accept_fence_model.sh`、`tools/verify_fence_model.py`。生成场景、网格、材质、贴图、播放器沿现有工程忽略规则保留本地；源手绘贴图、专用源码与本目录证据支持复现。
