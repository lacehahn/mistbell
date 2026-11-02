只做一件事：加**云雾显现**——玩家从山脚往上爬时，教堂先在雾里，越走近越清晰（这是这个游戏的核心体验）。不改美术材质、山体、贴图、机位表。

必读：`Assets/Scripts/PlayerController.cs`、`Assets/Scripts/InfoPanel.cs`、`spike/project/Assets/Editor/SpikeScene.cs`（雾与 Volume 设置）、`docs/00-vision.md`（"钟声先穿过云雾到达，教堂随后显形"）。

【新增 1 个脚本】
`Assets/Scripts/MistReveal.cs`：
- 参考物：教堂锚点（世界坐标 ~(0, 0, −9)）与玩家位置，计算水平距离 `d`。
- 随 `d` 变化（d 从 95 m → 20 m）**线性/平滑插值**：
  - `RenderSettings.fogStartDistance`：120 → 20
  - `RenderSettings.fogEndDistance`：420 → 120
  - `RenderSettings.fogColor`：`#CBD8E4`（冷灰雾）→ `#EAF0F6`（亮雪雾）
  - 环境光 sky：`#64748C` → `#849DBB`
- **钟声时刻**：首次越过 `d = 45 m` 时，触发一次 1.2 秒的"脉冲"：Bloom intensity ×1.6、Vignette ×1.3，然后 0.8 秒回落（用代码插值，不新增资产）。只触发一次。
- 参数集中成常量并打印一次：
  `[REVEAL] fog=120..420→20..120 color=#CBD8E4→#EAF0F6 bell_at=45m`

【挂载】在 `SpikeScene.Run` 里把该脚本挂到场景（例如玩家对象上或独立 GameObject），并确保**带 `-shot`/`-shots` 参数时完全不生效**（截图链路不许被改）。

【必须自己跑并给证据】新增演示参数
1) `-demo-reveal`：依次把玩家放到 d = **80 / 50 / 20 m** 三个位置（直接设位置，不用走），每个位置等 0.5 s 稳定后截图：
   `Shots/reveal_80.png`、`Shots/reveal_50.png`、`Shots/reveal_20.png`
   每张打印：`[REVEAL] d=<m> fogStart=<f> fogEnd=<f> churchEdge=<f>`
   其中 `churchEdge` = 截图中**教堂包围盒区域**内的高频能量（相邻像素差均值，0–1），你必须自己算。
2) **硬门槛**：`churchEdge(80) < churchEdge(50) < churchEdge(20)`，且 `churchEdge(20) ≥ churchEdge(80) * 1.6`。
3) 回归：`-demo-walk 6` 仍满足 distance ≥ 15 m、贴地误差 ≤ 0.02；`capture_runtime.sh` 仍 exit 0 且 `measure_shot.py` 的 `magenta_px = 0`。

【顺序】SpikeScene.Run → SpikeBuild.Run → -demo-reveal → -demo-walk 6 → capture_runtime.sh → measure_shot.py，退出码全 0。
不加包、不 commit、不改美术材质/山体/贴图/机位表/measure_shot.py。
