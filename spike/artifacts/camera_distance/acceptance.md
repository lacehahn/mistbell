# camera_distance 验收

严格整体结果：**未全通过**。尺寸、玩法构图、相机防穿测试、门洞/长椅碰撞、室外 FPS 达标；原 reveal 门槛仍失败。未改原门槛或 reveal 机位。

## 实现

- `PlayerController.cs`：PlayerHeight=1.50，FollowOffset=(0,2.10,-11.50)，LookHeight=1.20；修正启动48但随后回到35的问题，玩法FOV统一48。
- `SpikeScene.cs` 和局部入口 `CameraDistanceBuild.Run`：角色 _ScaleY=1.60、_ScaleX=1.28；24帧alpha可见高度1.50–1.52m。只修补既有角色材质尺寸，不重建室内/环境。
- `PlayerCameraDistance.cs`：实际玩法相机的alpha边界投影像素验收与截图/往返测试；没有另设截图镜头。归一到1080p输出，同时断言截图实际1080p。
- 原 `MistReveal.cs` SHA256 与执行前一致：`da56ef9f5e20e85d238f55453d2149077367c5926126bc1e4e63ec34837e7f5a`。其他受保护源文件无git差异。未修改AGENTS.md、任务书；未提交。

## 实际执行

```sh
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$PWD/spike/project" -executeMethod CameraDistanceBuild.Run -logFile "$PWD/spike/logs/camera_distance_build.log"
bash spike/tools/accept_camera_distance.sh --player-only
python3 spike/tools/verify_camera_distance.py
git diff --check
```

构建Succeeded / 0errors。验收入口串行执行 `-demo-camera-distance`、`-demo-walk 6`、`-demo-enter-church`、`-demo-reveal`、`-bench -bench-foreground`；均1920×1080。验收及复核脚本exit=1，如实拒绝全绿。diff检查通过。

## 数字

```text
[CAM_FRAME] dist=11.50 h=2.10 look=1.20 fov=48.00 pilgrim_px=158.478 pilgrim_pct=14.674 actual_h=1080 contained=True
[CAM_CLEARANCE] player_h=1.5 diameter=0.6 doorway_head_gap=2.06461 pew_pitch=0.80230 back_gap=0.70867
[CAM_TEST] phase=0 seconds=6.000 samples=352 bad=0 ground_gap=3.53063 wall_gap=3.99951 turned=true
[CAM_TEST] phase=1 seconds=6.017 samples=360 bad=0 ground_gap=3.10025 wall_gap=3.99951 turned=true
[PLAY] walk_start=(-18.000,-28.835,-205.000) walk_end=(-18.000,-26.368,-189.804) distance=15.395 row=3 fps_ok=true
[PLAY] ground_error_max=0.000000 raycast_misses=0 fps=60.00 animation_fps=10 frames=6
[INT_PASS] light=True domeLight=True ceilingSeen=True domeSeen=True fx=0.785 visible=True obstacles=True walked=True z=0.43 dy=0.000 fov=50.00 lamps=20 shafts=3
[REVEAL] gate=FAIL ratio=1.284394 near_dist=28 church_height_pct=0.620000 ratio20_80=1.284394 bell_once=True pulse_restored=True
```

- demo-walk 6：bad=0；demo-enter-church：bad=0；demo-reveal：bad=1。
- 原长椅行距0.80230m不改；旧净空0.28m小于角色直径0.60m，仍不可横穿两排。实际1.5m胶囊的长椅、墙、柱、祭坛、圣像屏碰撞测试均pass，进堂walked/obstacles=True。back_gap=0.70867仅为靠背之间距离，不冒充整排净空。
- 室外z01：226.26FPS / 1%low 169.96 / 283drawcalls；terrace：223.97FPS / 1%low 161.45 / 352drawcalls。任务≥200FPS通过。硬件Apple M6 / Metal，vSync0，不限帧，预热≥180帧且≥3秒，采样300帧。
- 通用BENCH_GATE=False：台地352drawcalls高于原330限制；本任务没有要求新的drawcall预算，也未改原通用门槛。室内215.09FPS / 250drawcalls，仅记录回归结果。

## 截图目视复核

[cam_play_1080.png](../../project/Shots/cam_play_1080.png) / [cam_walk_1080.png](../../project/Shots/cam_walk_1080.png)，均1920×1080。静止截图位置是路径中心z=-110，使用原雾；角色约屏高1/7，弯曲道路及栏杆引导线清楚，教堂完整在上半部。教堂网格投影上下边界y=0.96577/0.71239（底部为0），左右x=0.44967/0.60631，完整可容纳。行走截图在同一起点往返转身六秒后捕获，人物已转向镜头。另在台地旁z=-23独立测试往返六秒，无相机穿地/穿墙。

这不表示靠近教堂时所有距离都能完整装下建筑：z=-32和z=-80的初次构图会截顶部，因此未用那些图作为完整教堂的验收图，也没有抬高镜头或改reveal来掩盖。当前任务规定的相机参数均在指定区间。

## 保留失败

reveal本轮比值低于1.6；历史 `far_mist_rocks` 已记录1.311026<1.6。本轮继续保留原机位、雾、远山、材质和断言，不扩展范围修复。详细逐项结果及截图SHA见[measurements.json](measurements.json)，原始日志见 `spike/logs/camera_distance_*.log`。
