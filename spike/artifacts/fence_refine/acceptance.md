# 围栏细节修正验收

仅执行 `spike/tasks/fence_refine.md`。命令 `bash spike/tools/accept_fence_refine.sh` 最终 exit=0，独立验收 35/35 项通过，构建 Succeeded / errors=0。以下尺寸冲突取舍必须与验收结论一起阅读。

## 尺寸冲突与实现取舍

任务书的柱距 2.2 m、柱宽 0.09 m、横杆长 2.14 m、每端嵌入 0.03 m 无法同时成立：`2.2 - 0.09 + 2 × 0.03 = 2.17 m`；2.14 m 只能嵌入每端 0.015 m。已在主会话提出选择，未收到答复，暂优先采用明确列入验收项的 0.03 m 嵌入、保留柱距和柱宽，标准直线跨的水平横杆长度为 2.17 m。**这与任务正文的 2.14 m 不一致，不声称四个尺寸全部满足。** 曲线/倾斜柱按真实接头裁切；本场景斜坡上实测三维杆长 2.176402–2.223792 m。

## 几何与渲染

- 181 跨、183 根共享柱，两侧各仅一排；A/B/C=146/18/17。每跨只含起点柱，每侧末端增加一根封口柱；相邻跨引用同一终点/起点柱。
- 沿已收窄到 2.1 m 的道路 `ClimbRoute.SmoothPoints` 重排，距中心线 1.4 m；采样步长 2.2 m，曲线弦长 2.198608–2.200017 m。真实围栏/路面三角形独立 SAT 复算相交数=0。
- 端点按实际共享柱中心、柱面及各杆高度连接，嵌入 0.029970–0.030033 m（独立复算，±0.001 m 验收容差）；最大悬空间隙 0.000000 m。所有横杆端面角点均在所属柱截面内，没有穿透到另一侧。
- B 柱外倾实测 4.024573–5.883665°；横杆分别连接两端柱在 0.88/0.38 m 高度处的实际中心，整跨随斜度变化。C 整根缺少下横杆，保留完整上横杆及其两端支撑。
- A/B/C 每跨 [88, 88, 64] tris；含末端柱总 15600 tris；合并 1 个 Renderer/材质。合批数与全帧 drawcall 分开统计。
- 柱截面 0.09 × 0.09 m、高 1 m；柱顶雪帽 0.10 × 0.10 × 0.035 m、平面四角 0.01 m 倒角，共 183 个；横杆顶雪条 0.06 × 0.02 m、沿杆长铺满，共 345 条。雪色 #EDF3F8 转线性顶点色，复用原围栏材质，无新贴图/材质。
- 共用现有 `fence_wood` 来源的 `Fence_Wood_1024.png`。所有木材六面 UV 非退化，侧面纹理 V 沿长轴；柱竖纹、杆横纹，每跨随机 UV 偏移 0–1。UV 退化三角形=0，长轴方向与真实几何独立比对通过。
- 独立检查闭合体有向体积均为正，排除反面雪盖/木面。

## 截图与像素

[3 m 近景](fence_closeup.png) / [玩法 1080p](cam_play_1080.png)，均本轮新生成 1920×1080，洋红像素=0。已逐图目视：木纹、柱帽、杆顶雪条可辨，C 为整根缺杆；没有旧版半空断杆或成对贴柱。近景仅专用检查会话临时使用 FOV57、关闭后期，原玩法/reveal 参数与后期均未改。

实际网格射线命中、UV 与同帧 PNG 采样导出到 `fence_closeup_pixels.csv` 和 `fence_closeup_snow.csv`。木纹三阶 L* 中位数/样本数：[(40.927864, 776), (41.897887, 3102), (45.368913, 134)]；雪面 1048 点，L* 中位数 100.000004。不会把材质数值当作截图测量。

## 性能与演示

Apple M6 / Metal / Unity 6000.3.27f2 / 1920×1080 Development，`open -n -a` + caffeinate 启动。沿用现有 PlayerBenchmark：至少 180 帧且 3 秒预热，300 帧采样，vSync=0、不限帧。200 FPS 门槛指平均值，1% low 如实列出。

| 测点 | 平均 FPS | 1% low FPS | 全帧 drawcall |
|---|---:|---:|---:|
| z01 | 214.10 | 166.27 | 207 |
| terrace | 222.72 | 167.57 | 276 |
| interior | 215.70 | 160.70 | 256 |

三项演示最终 walk/enter/reveal bad=0。walk6 距离 15.394 m、贴地误差 0、漏射线 0；INT_PASS 全 True；reveal gate=pass、bell_once=True、pulse_restored=True、fence_feet=183。玩法相机两阶段 CAM_TEST bad=0，保持 11.5/2.1/1.2/FOV48。

首轮删除了重复柱的碰撞代理，触发原揭示演示 `feet=0` 失败；已恢复每根共享柱恰好一个柱脚代理，精确倾斜网格仍由合并 MeshCollider 负责。保留失败日志 `logs/fence_refine_pre_support_reveal.log`，未修改 reveal 检测器或放宽门槛。最终整套重跑通过。

## 保护与复现

路面几何/贴图/海拔混合、室内、祭坛、人物、玩法相机、reveal 机位、雾与山的非围栏组件/网格/材质保护摘要前后一致：`A78036A1A0AA88F9083EE625CED21A46B7FA52DDAEB9D07C5B13B73F25F917F5`。没有修改 AGENTS.md、任务书或旧任务验收脚本，没有安装依赖或提交。

```sh
bash spike/tools/accept_fence_refine.sh
# 已完成构建时，仅复测播放器和独立验收：
bash spike/tools/accept_fence_refine.sh --player-only
# 已有同轮完整日志与截图时，独立复算：
python3 spike/tools/verify_fence_refine.py
```

[原始几何/UV/雪色](assets.json) / [测量结果](measurements.json)。原始日志为 `spike/logs/fence_refine_{build,camera,closeup,walk,interior,reveal,bench,acceptance}.log`，性能 JSON 为 `spike/logs/fence_refine_bench.json`。
