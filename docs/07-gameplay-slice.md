# 07 · 可玩切片（M1 验证）

> 状态：**可玩**。走路 → 走近云雾中的教堂 → 到门口按 E 读介绍。
> 记录时间：2026-11-02 15:10

## 1. 怎么玩

```bash
# 构建产物已存在：
open spike/project/Builds/Mac/Mistbell.app
```

| 操作 | 键 |
| --- | --- |
| 移动 | `W A S D` / 方向键 |
| 加速 | 按住 `Shift`（3.0 → 5.0 m/s） |
| 交互 | `E`（进入 POI 半径时左下角出现「按 E 查看」） |
| 关闭面板 | `E` 或 `Esc` |

带 `-shot` / `-shots` 参数启动时**完全走截图模式**，不会启用玩家控制（保证美术验收链路可复现）。

## 2. 现在能证的三件事

| 体验目标 | 实现 | 验收证据 |
| --- | --- | --- |
| **攀爬** | 贴地行走、四向 6 帧动画、跟随相机 | `[PLAY] distance=18.106 m ground_error=0.000000 row=3`（6 秒） |
| **云雾显现** | 距离驱动的雾（120..420 → 20..120 m）、45 m 处钟声脉冲 | `[REVEAL] gate=pass ratio20_80=1.743754 bell_once=True`；三档截图 `Shots/reveal_{80,50,20}.png` |
| **景点交互** | 数据驱动 POI（`PoiData` + `PoiDatabase`），面板中英双语 | `[POI] count=3 ids=gergeti_p01..p03`；截图 `Shots/poi_p0{1,2,3}.png` |

## 3. 三个 POI

| id | 名称 | 锚点 | 半径 |
| --- | --- | --- | --- |
| `gergeti_p01` | 正门 · Main Portal | (0, y, −13) | 3.5 m |
| `gergeti_p02` | 钟楼 · Bell Tower | (10.5, y, −2) | 3.0 m |
| `gergeti_p03` | 门廊浮雕 · Portal Relief | (0, y, −14.5) | 2.5 m |

文案遵循 docs/02 的「标题 + 2–4 段 + 出处」口径；中文用显式 `\n` 手工排版（Unity legacy Text 会在空格与中日韩/数字边界断行，自动换行不可控）。

## 4. 复现命令

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
$UNITY -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run  -logFile spike/logs/scene.log
$UNITY -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run  -logFile spike/logs/build.log
APP=spike/project/Builds/Mac/Mistbell.app/Contents/MacOS/Mistbell
caffeinate -dimu $APP -demo-walk 6      # 走路回归
caffeinate -dimu $APP -demo-reveal      # 三档云雾截图
caffeinate -dimu $APP -demo-poi gergeti_p02   # 指定 POI 面板截图
caffeinate -dimu bash spike/tools/capture_runtime.sh   # 美术机位截图
python3 spike/tools/measure_shot.py spike/project/Shots/art_runtime.png
```

## 5. 还没做的（明确边界）

- **多 Zone 路线**：文档里是 6 个 Zone / 25 分钟，切片只有 1 段坡 + 广场。
- **呼吸与体力**：文档 02 有取舍，切片未实现。
- **音频**：钟声/风/脚步为程序化合成，尚未做混音与空间感。
- **室内**：未做（D02 待核实室内结构）。
- **存档 / 设置 / 本地化切换**：未做。
