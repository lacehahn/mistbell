# Fence / path tone 验收记录

任务：`spike/tasks/fence_path_tone.md`。执行 `bash spike/tools/accept_fence_path_tone.sh`，退出码 **0**，22 项检查全 true；`git diff --check` 通过。场景原地修补，未调用全场景生成器，未改回归门槛。

- `[FENCE3] spacing=2.199987..2.200088 count=185 crossing=0`；每侧一排。沿坡最大柱距 2.255011m；柱体 0.08×0.08×1m，顶雪 0.04m，材质 #4A3B2E、roughness=.85、diffuse only。旧 8 个渲染批次替换成 2 个；源节点只保留碰撞，不重复渲染。跨折角的栏杆在路缘折点分段，针对含雪边的全部路面三角形作 XZ 相交检查，0 段穿越。
- `[PATH3] luma=54.173238 b_star=2.560099 edge_ok=true`。近景同像素旧值 L*=49.378127，增亮 **9.71%**。路面保留地形贴合的连续顶面，无 Box 侧面、无中央接缝，7cm #E8EFF6 雪压边与顶面共网格、向外落至雪地；不投射路缘硬阴影。
- `[SNOW3] ridges=3`：长 4/4.5/5m，宽 2m，中心抬升约 .197/.222/.247m（包含 .003m 边缘埋入），余弦平方起收，几何体积，不加噪点。
- 1920×1080 / Apple M6 / Metal：z01 **212.44 FPS / 269 drawcalls**，台地 **221.41 FPS / 328 drawcalls**，室内 **216.25 FPS / 256 drawcalls**；对应 1% low 为165.35/160.88/158.13 FPS。使用原 benchmark 180帧且至少3秒预热、300帧采样；200 FPS 门槛指平均值。
- `-demo-walk 6`：15.395m，ground_error_max=0、raycast_misses=0，bad=0。
- `-demo-enter-church`：INT_PASS 所有布尔 true，bad=0。
- `-demo-reveal`：gate=pass、bell_once=True、pulse_restored=True，bad=0。沿用当前构图门槛，无修改 reveal 代码/机位/雾；ratio=1.272898 为原日志的参考量。
- 玩法相机11.5/2.1/1.2/48，两阶段 CAM_TEST bad=0。室内、祭坛、人物、镜头、雾、山及其他非任务组件/网格/材质的前后摘要一致：`57AA9C831B9694D849168D86396C7934FE9BEC880E6543C098BAC3F949347208`。首次修补与重复运行均一致。AGENTS.md 未修改。

## PNG 颜色测量口径

先用真实表面网格射线首命中导出像素坐标 CSV，再由 Python/Pillow 从最终 PNG 按 sRGB → XYZ → CIELAB D65 独立重算，未按颜色筛选。近景路面 70599 个内部像素；受光木柱 308 个像素，平均 L*=22.241191、最高 **22.870519**。木柱只取距镜头18m内、距柱顶底至少30cm、法线朝阳且四方向4px仍命中同柱面的内部，排除雪帽和轮廓。`path_closeup.png` 保持原检查机位3m/post=false，受光面亮度按该无后期近景验收。玩法图同类柱面平均L*=20.761965，最大L*=52.069778；最大值含前景景深把雪背景混入细柱的像素，原样保留于 measurements.json，不将其当成无混色的材质受光面。

当前项目工作色空间为 Gamma；仅为围栏/路面新增专用 shader，未修改旧材质所用 shader。新诊断只在 `-fence-path-tone-audit` 标志下运行，普通游戏和性能测试不运行像素射线测量。

## 文件与复现

- `cam_play_1080.png`、`path_closeup.png`：本次最终截图，1920×1080，已目视检查；保留 before 两图。
- `measurements.json` / `assets.json` / 两份 `*_tone_pixels.csv`：全部数字、坐标、摘要及判据。
- `../../logs/fence_path_tone_acceptance.log`：最终验收输出；同前缀 build/camera/closeup/walk/interior/reveal/bench 日志及 bench.json 为本轮原始证据。
- `../../project/Assets/Editor/FencePathToneBuild.cs`：从当前已制作场景原地应用补丁、保存并打开发包；新资产在 Assets/Settings/FT3_* 和 M_FT3_*。场景及 Settings 遵循仓库原有忽略规则，可由上述验收命令重建补丁。
