# 路面收窄与弯道平滑验收

仅执行 `spike/tasks/path_width_bend.md`。最终验收 `pass=true`，构建 `Succeeded / errors=0`。

- 总宽（包含两侧各 7 cm 雪压边）：2.099995–2.100008 m；石板主体 1.96 m。与 1.5 m 角色的比例为 1.4。
- 二次贝塞尔切线过渡，约 1 m 等距采样，共 208 点；最小转弯半径 21.216383 m；最大相邻段夹角 2.697220°。
- 路面自交 0，挡土墙重叠 0，围栏穿路 0。
- 围栏起柱间距 2.198608–2.200017 m，柱心至含雪边路缘约 0.35 m；复用 A/B/C 模型、材质与单批合并。
- 静态脚印全部回到路内，最大中心偏移 0.180000 m。静态脚印按沿路位置每 8 个合批；动态脚印保留 120 容量、0.75 m 间隔与原材质，改为单个动态网格提交，避免收窄后雪地接触增多造成 drawcall 超限。
- 原高度函数、地形不变；爬升实测前后均为 27.442068 m（出生点至终点）；终点支撑高度前后均为 -1.392631 m，差值均为 0。
- 石板纹理、2.4 m 世界投影、海拔混合、人物、室内/祭坛、玩法与 reveal 机位、雾和山均未改动；场景受保护对象前后摘要一致。未修改 AGENTS.md，未提交。

| 验收项 | 实测 |
|---|---|
| `-demo-walk 6` | 15.396 m；贴地误差 0；raycast_misses=0；bad=0 |
| `-demo-enter-church` | INT_PASS 全 True；bad=0 |
| `-demo-reveal` | gate=pass；bell_once=True；pulse_restored=True；bad=0 |
| 补充 `-demo-trail 10` | spawns=33；walk_pass=true；bad=0；已目视确认可见 |
| 1920×1080 z01 | 212.58 fps；207 drawcall |
| 1920×1080 terrace | 220.85 fps；276 drawcall |
| 1920×1080 interior | 216.26 fps；256 drawcall |

性能为 Apple M6 / Metal 开发构建，沿用现有 benchmark：180 帧且至少 3 秒预热，300 帧采样，关闭垂直同步与帧率上限。室外两点均 ≥200 fps，台地 276 drawcall ≤330。

复跑完整构建和验收：

```bash
bash spike/tools/accept_path_width_bend.sh
```

独立复算（读取导出的顶点、PNG、运行日志和性能 JSON）：

```bash
python3 spike/tools/verify_path_width_bend.py
```

日志：`spike/logs/path_width_bend_build.log`、`path_width_bend_acceptance.log` 和同前缀各演示日志。原始数据：[assets.json](assets.json)，复算结果：[measurements.json](measurements.json)。

截图均为本轮新生成的 1920×1080，magenta=0；已目视检查路宽、平滑弯道、路缘、围栏与脚印。

- [cam_play_1080.png](cam_play_1080.png)
- [path_closeup.png](path_closeup.png)
