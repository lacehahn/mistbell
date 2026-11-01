# 修正任务（队列 4 第 2 轮）：远山被雪和雾洗白了，方向对但参数错

## 我看了 `Shots/art_runtime.png` 的判定

你做的三件事（RidgeFog shader / 线性雾收紧 / mist.png）机制都在跑，四条日志都打出来了。
但**画面比改之前更差**，三个具体原因：

1. **雪线高度设错 → 整座山是白的。** 山脊顶点世界高度只有 200–400 m，
   而你在第 2 层用 `snowHeight=110`、第 3 层用 `250`，等于把山体 90% 都判成雪。
   现在远山是一排苍白纸片，和天空几乎同色，三层轮廓**分不开**。
2. **雾过重。** `fogEnd=650` 而远山在 250/450/700 m：第 1 层已 33% 雾化，第 3 层等于 100%
   融进雾色 → 最深那层彻底消失，纵深反而没了。雾色的选择也要往天空色靠（见下）。
3. **雾带在相机背后。** 你说得对，我的简报写错了方向：相机在 z=-100 朝 **+z** 看，
   所以雾带必须在 **z = +170 / +330**，不是 -170/-330。这条是我写错的，按下面改。

## 只允许改这些文件

- `spike/project/Assets/Editor/SpikeScene.cs`（雾参数、`BuildMist()` 坐标、`BuildMountains()` 的层配色/雪线参数）
- `spike/project/Assets/Art/Shaders/RidgeFog.shader`（只在需要时改：雪线软过渡、山脚额外雾化）
- **不要**动机位表、`Fov`、教堂、角色、后期 `BuildRuntimeProfile()`、`make_textures.py` 已有的三类贴图

## 具体参数（照这个改，别再自己推）

**雾**（`RenderSettings`）：
- `fogStartDistance = 90`、`fogEndDistance = 1500`
- `fogColor = #A7B7CB` —— 就是天空色（`sky` 区域实测 (167,183,203)）；
  这样最远那层山自然收进天色，不会出现「山比天还亮」的纸片感

**三层山**（`BuildMountains()`；雪只放最远两层，且只盖住峰顶）：

| 层 | 距离 | bottom | top | snowHeight | softness |
| --- | --- | --- | --- | --- | --- |
| 1 | 250 m | `#44546A` | `#7A8C9E` | 不启用 | — |
| 2 | 450 m | `#5A6D80` | `#93A5B6` | **330** | 45 |
| 3 | 700 m | `#74879A` | `#A9B8C6` | **300** | 60 |

- 雪线必须**只有峰顶一小片**：山体在画面上超过 1/4 变成白 = 参数又错了
- 山脚额外雾化 `_FogFloor` 保留，但**只作用于山脚 1/3 高度**（现在的做法看起来是整层压白）

**雾带**（`BuildMist()`）：
- 近带：`z = +170`、`y = GroundY(170) + 6`
- 远带：`z = +330`、`y = GroundY(330) + 14`
  （`GroundY` 是场景里现成的解析式；注意它按 z 给海拔，正 z 在坡上方）
- 尺寸 900×380、alpha 0.22 / 0.30、`ZWrite` off、透明队列；tiling 3×1 与 5×1
- 雾带**不许**从教堂（z≈-11..13）和角色（z≈-80）前方穿过，只出现在它们后面

## 验收（分步跑，贴关键输出）

```bash
cd /Users/lance/workspace/mistbell
pkill -f "Unity.app/Contents/MacOS/Unity" ; sleep 2
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/82_scene.log ; echo SCENE=$?
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/83_build.log ; echo BUILDAPP=$?
caffeinate -dimu -t 180 bash spike/tools/capture_runtime.sh
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png
```

必须同时满足：

1. `SCENE=0`、`BUILDAPP=0`、`error CS` 计数 0，三张 `art_*.png` 都重新落盘（看 mtime）
2. `frame church heightPct ∈ [45,55]`、`pitch ∈ [10,14]`、`frame porch centerX ∈ [760,1160]`
3. 三张图 `clipped>=250` 全部 **< 3%**
4. `sky` 均值仍 ≈ (167,183,203) ±10
5. 日志里有 `fog start=90 end=1500`、`mist near z=170 ... far z=330 ...`
6. 目视（你自己用 `--image=` 把 `spike/project/Shots/art_runtime.png` 读进来确认后再回答，不要凭感觉写「已目视」）：
   三层山**颜色深浅依次递减但都明显深于天空**、雪只出现在 2/3 层的峰顶、没有白色描边
