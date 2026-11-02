只做一件事：把交互从"写死 1 个点位"改成**数据驱动的 POI 系统**，并加 2 个真实点位（钟楼、门廊浮雕）。不改美术材质、山体、雪面、相机、雾。

必读：`Assets/Scripts/Interactable.cs`、`Assets/Scripts/InfoPanel.cs`、`Assets/Scripts/PlayerController.cs`。

【重构】
1) 新增 `Assets/Scripts/PoiData.cs`：`[System.Serializable] class PoiData { string id; string titleCn; string titleEn; string bodyCn; string bodyEn; Vector3 anchor; float radius; }`，用一个 `PoiDatabase` 存 4 个点位（含现有正门）。
2) `Interactable` 改为可配置多实例：每个 POI 一个触发器，进入半径显示「按 E 查看」，按 E 打开对应内容。
3) `InfoPanel` 增加 `Show(PoiData)`，标题/正文按数据渲染（中文用**显式 \n 手工换行**，每家 4–6 行，不要靠自动断行）。

【2 个新点位文案（照抄，不要改写）】
P02 钟楼 · `gergeti_p02`，锚点钟楼脚下 (10.5, y, −2)，半径 3.0 m：
中文：
「钟楼独立于主堂之外，这是高加索教堂的旧习：\n钟声要能先穿过山谷，而不是被墙挡住。\n格鲁吉亚的钟多为铁铸，音色偏低，\n风雪天传得更远——牧人靠它辨时辰，\n旅人靠它辨方向。」
英文：
「The bell tower stands apart from the nave, as Caucasian churches often do: the bell must carry across the valley rather than be muffled by walls. Georgian bells are usually cast iron, low in pitch, and travel further in snow—herders used them to keep time, travellers to keep their bearings.」

P03 门廊浮雕 · `gergeti_p03`，锚点正门台阶前 (0, y, −14.5)，半径 2.5 m：
中文：
「门楣上的十字与藤蔓是十四世纪的手艺：\n十字表信仰，藤蔓表生长，两者缠在一起，\n意思是「在此地活下去」。\n刻痕深浅不一，是几代石匠接力留下的。」
英文：
「The cross and vine carved into the lintel are 14th-century work: the cross for faith, the vine for growth, wound together to mean 'to live here'. The cuts vary in depth—several generations of masons worked this stone.」

P01 正门（现有）保留，id `gergeti_p01`，半径 3.5 m，文案沿用现文本。

【自检打印】
"[POI] count=3 ids=gergeti_p01,gergeti_p02,gergeti_p03 radius=3.5/3.0/2.5 snapped=true"
"[POI] open=<id> title_ok=true lines_cn=<n> screenshot=Shots/poi_<id>.png"

【必须自己跑】
`-demo-poi gergeti_p02` 与 `-demo-poi gergeti_p03`：各自把玩家瞬移到该 POI 前 2 m、触发交互并截图（`Shots/poi_p02.png`、`Shots/poi_p03.png`），面板必须打开且标题正确。
回归：`-demo-walk 6`（distance ≥ 15 m、贴地 ≤ 0.02）、`-demo-reveal`（三张图仍在）、`capture_runtime.sh` exit 0、`measure_shot.py` magenta_px = 0。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改美术材质/山体/雪面/相机/雾/measure_shot.py。
