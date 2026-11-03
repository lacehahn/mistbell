只做一件事：加**POI 已读标记 + 札记列表**（读过的点能回看，未读的有提示）。不改美术、不改玩法数值、不改相机。

必读：`PoiData.cs`、`InfoPanel.cs`、`PauseMenu.cs`、存档实现（`mistbell_save.json` 的 `visitedPois`）。

【要加】
1) **面板里显示已读**：打开已读 POI 时，标题右侧加一个小标记「已读」，正文区顶部加一行浅色 `已读 · 第 N 次打开`。
2) **札记列表**：设置菜单（Esc）里加一项「札记 Notes」，打开列出全部 POI：`已读/未读` + 标题（中英按语言设置）。未读条目显示为灰色「？？？」，已读条目可点开回看正文（复用 InfoPanel 的渲染，不新建 UI 体系）。
3) **未读提示**：进入未读 POI 半径时，「按 E 查看」改为「按 E 查看 · 新发现」。
4) **持久化**：已读列表写进现有存档（`visitedPois`），新增 `visitCounts`（map id→次数）。

【自检打印】
"[JOURNAL] total=<n> visited=<n> unread=<n> visit_counts=<k> label_unread=<bool> menu_entry=true"
硬门槛：`total = 8`、已读面板出现「已读」字样（用 UGUI 文本检测）、未读提示出现「新发现」、`visitCounts` 与存档一致。

【必须自己跑】
新增演示 `-demo-journal`：打开 p04 → 关闭 → 再打开 → 打开札记列表 → 截图 `Shots/journal_list.png` 与 `Shots/poi_p04_read.png`；打印上述指标。
回归：`-demo-enter-church`、`-demo-save`、`-demo-load`、`-demo-settings`、`-demo-walk 6`、`capture_runtime.sh`、`measure_shot.py`（magenta_px=0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改美术/玩法数值/相机/measure_shot.py。
