# 石板路贴图验收

2026-11-04，任务范围：[path_texture.md](../../tasks/path_texture.md)。最终验收 `pass=true`，播放器验收命令 exit=0；Unity 构建 Succeeded / errors=0。

## 改动

- `M_Paving` 改用专用 `Mistbell/PathPainted` shader。裸石 / 雪压原图均保留 1254×1254，sRGB、Bilinear、Repeat、mipmap、aniso=4；主投影每铺 2.4 m，+37°；第二档 3.366 m，−37°，权重 0.32。均使用世界坐标，不使用 Box UV。
- 台阶基脚按既有构建公式采样：y=−1.35763 m。裸石端 y≤−11.35763，雪压端 y≥−3.35763，中间 8 m 线性混合。
- 原路两排 Box 的 6 cm 连续中央缝露雪，左右采样高度差又暴露亮侧面。原 270 块 Box 保留碰撞体与原始变换，关闭其渲染，以 1048 顶点 / 1046 三角形贴地连续路面替代可见部分；路面不投影，仍接收阴影。避免旧大格接缝和中央亮线。
- 独立亮边对象 `PathSnowEdgeV2 / M_PathSnowEdge` 已从场景删除；原场景探针证据见 [white_source.log](white_source.log)。
- 阶石素材裁剪框为原图左上坐标 `(0,338)–(1280,450)`，生成 1280×112 单条踏面图；U Repeat、V Clamp。仅九级 `TerraceStep_0..8` 的朝上 720 个三角形使用该材质，竖面保持原材质、碰撞不变。
- 非路面/台阶组件及所用材质的修改前后 SHA256 相同，见 [assets.json](assets.json)。未改室内、祭坛、人物、玩法相机参数、围栏、雾或 AGENTS.md。

## 最终实测

1920×1080，Apple M6 / Metal，Development Player，前台，vSync=0、300 帧采样。FPS 为无帧率上限的吞吐测试；正常试玩仍沿用原帧率设置。

| 位置 | 平均 FPS | 1% low FPS | drawcall |
|---|---:|---:|---:|
| z01 | 212.32 | 165.37 | 281 |
| terrace | 222.98 | 165.73 | 328 |
| interior | 215.78 | 161.04 | 256 |

- `-demo-walk 6`：distance=15.395 m，ground_error_max=0，raycast_misses=0，bad=0。
- `-demo-enter-church`：INT_PASS 全部 True，obstacles=True，lamps=20，bad=0。
- `-demo-reveal`：gate=pass，bell_once=True，pulse_restored=True，bad=0。
- `-demo-camera-distance` 两阶段 bad=0；玩法相机距离11.5 m、高差2.1 m、look=1.2 m、FOV=48°。
- `path_closeup.png`：相机距采样路面3.000 m。五张验收图均1920×1080、magenta_px=0，mtime晚于本次构建。
- 目视：石板形状可辨，上段明显更白；无贯穿白线、无旧 Box 大格线、未见明显平铺重复网格。近景检查关闭后期；玩法截图保留原后期和雾。

## 截图与复验

- [路面3m近景](path_closeup.png) / [玩法机位](cam_play_1080.png)
- [下段裸石](path_lower.png) / [上段雪压](path_upper.png) / [九级台阶](path_steps.png)
- [验收日志](acceptance.log) / [完整数字与图像SHA256](measurements.json) / [性能原始数据](bench.json)

```sh
# 准备贴图、局部修补场景、构建并运行全部任务验收
bash spike/tools/accept_path_texture.sh
# 对当前构建复验（本轮最终命令，exit=0）
bash spike/tools/accept_path_texture.sh --player-only
```

仅新增本任务构建入口 `PathTextureBuild.Run`、专用 shader、检查用截图脚本及验收脚本；不重建全场景。生成资产位于仓库既有忽略目录 `Assets/Art/Textures/Path/`、`Assets/Settings/` 与 `Assets/Scenes/`，均已实际写入工作区；未提交。
