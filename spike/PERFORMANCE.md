# 1080p 性能实测（2026-11-03）

最终基准门槛通过：三处各 300 帧，平均 FPS ≥55、draw calls ≤250。最终构建、基线采样、基准复测、三个演示、截图脚本与 11 次图像量测全部退出 0，11 张图全部 magenta_px=0。

环境：Mac mini / Apple M6（12 核 GPU）/ 32 GB 统一内存，macOS 27.0，Unity 6000.3.27f2，Metal，1920×1080 窗口。Development 构建提供 ProfilerRecorder 计数器，无 Deep Profiling、无连接 Profiler。

**呈现条件必须随结果一起引用**：前后都使用 -force-gfx-direct；-bench 在本次进程关闭 VSync 和帧率上限，以测吞吐。三个演示配套临时 -perf-unpaced 参数；capture_runtime.sh 保留原呈现设置，仅使用其已有 MISTBELL_GFX_DIRECT=1 入口。正常启动的 VSync 设置未改。早期 VSync=1 的探索性结果明显波动且未满足 55 FPS，日志保留；此处无 VSync 结果不代表默认呈现模式已保证 55 FPS。

每处预热至少 180 帧且至少 3 秒，再采 300 个真实 Time.unscaledDeltaTime。平均 FPS=300/总耗时；1% low=最慢 3 帧耗时均值的倒数。Draw Calls Count、Triangles Count 为 300 帧计数器值的平均并四舍五入。截图在采样后读回，不另调 Camera.Render。

| 位置 | FPS 前→后 | 1% low 前→后 | draw calls 前→后 | tris 前→后 | 纹理内存 MiB 前→后 |
|---|---:|---:|---:|---:|---:|
| z01 | 187.66 → 260.14 | 35.17 → 40.14 | 6517 → 174 | 3011000 → 3092148 | 112.71 → 112.71 |
| terrace | 250.05 → 242.79 | 55.12 → 51.97 | 1048 → 220 | 3240659 → 3407490 | 112.71 → 112.71 |
| interior | 292.24 → 264.33 | 57.64 → 55.83 | 877 → 206 | 3046902 → 3441434 | 112.71 → 112.71 |

vram_mb 来自 ProfilerCategory.Memory 的 Texture Memory，以 1024² 换算。本机计数器可用；该值是纹理内存，不是 GPU 总内存，也不完整反映合并网格缓冲的占用。实际 API 命名空间为 Unity.Profiling.ProfilerRecorder。

位置：Z01 使用 ClimbRoute.Spawn 并落到现有地形；台地前 x=-2、z=-16；中殿 x=-2、z=0.5、y=FloorY。相机、FOV 和室内切换仍由原有 PlayerController.LateUpdate 处理。

三处基线 draw calls 均 >400，因此执行合批。2626 个静态源 Renderer 合成 17 个网格，源几何逐组断言 304871 → 304871 面。218 块室内地板按材质分到 4 个网格，保留全部材质色差。176 份脚印材质核对 shader、关键字、纹理及全部属性后共享。原对象与碰撞体保留，源 Renderer 使用 forceRenderingOff 防止重复绘制。

6 张长椅另合为一个双材质网格，7320 → 7320 面；ChurchInterior 在原有室内/室外切换时同步批次材质，原碰撞体不变。构建回调只在构建场景副本中去掉 29 个 TerraceWall 的 BatchingStatic 标记，使其源网格可参与显式合并；不保存源场景、不修改模型资产。

远山、天空保留 ShadowCastingMode.Off。运行时代码没有额外 Camera.Render；PlayerController.Start 的 Collider 查找已经缓存到 blockers 字段，其他全场景查找位于启动或演示验收路径。未改变模型面数、贴图分辨率、灯光参数、后期强度、相机机位或玩法。

**取舍**：合并使剔除粒度变粗，送绘 tris 上升；该计数含深度、阴影等多个 pass，不等于唯一模型面数。台地、室内平均 FPS 没有提升，不宣称全面提速。没有为压到 350k 而删面或改光照。跨多个外部区域的大范围合批曾进一步增加 tris，已撤回。

复现命令（从仓库根目录，沿用当前 ArtSpike.unity）：

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -quit -projectPath "$PWD/spike/project" -executeMethod PerformanceBuild.Run -perf-build-baseline -logFile "$PWD/spike/logs/perf_build_before.log"
spike/project/Builds/Mac/Mistbell.app/Contents/MacOS/Mistbell -force-gfx-direct -bench -bench-baseline -bench-no-batching -bench-output "$PWD/spike/logs/perf_before.json" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/perf_before.log"
"$UNITY" -batchmode -quit -projectPath "$PWD/spike/project" -executeMethod PerformanceBuild.Run -logFile "$PWD/spike/logs/perf_build_final.log"
python3 spike/tools/verify_performance.py
```

-bench-baseline 允许保存未达标的真实基线并返回 0；普通 -bench 未达标返回 1。-bench-no-batching 关闭运行时合批，基线构建配套 -perf-build-baseline。-bench-before 指定前次 JSON，逐位置打印 BENCH_OK；-bench-output 保存结果与采样后的 PNG。

最终证据：[基线日志](logs/perf_before.log)、[优化后日志](logs/perf_after.log)、[完整命令/退出码/FRAME md5/MEASURE](logs/perf_validation.log)、[构建日志](logs/perf_build_final.log)、[before.json](logs/perf_before.json)、[after.json](logs/perf_after.json)。探索性失败日志保留在同目录，最终验收以 perf_validation.log 的 pass=true 为准。

最终 -bench、-demo-enter-church、-demo-reveal、-demo-walk-to-tower、capture_runtime.sh 与 11 次 measure_shot.py 均返回 0。终端与日志逐图相邻打印 FRAME md5 和 MEASURE。未加包，未 commit，未修改 AGENTS.md、measure_shot.py 或美术资产。
