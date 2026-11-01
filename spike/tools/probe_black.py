#!/usr/bin/env python3
"""差分探针：把灌木材质的反照率置 0，量「剩下的亮度」从哪来。

背景：本轮 specular 已在材质里关掉（`_SPECULARHIGHLIGHTS_OFF` 进入 m_ValidKeywords），
但灌木亮度仍高于反照率 5 倍于它的草地（101 vs 77）。用「反照率归零」做差分：
  - 若归零后灌木矩形几乎变黑 → 亮度全部来自 diffuse，亮度比这把尺子量的是「受光角度差」，不是病
  - 若归零后仍有可观亮度 → 来自间接光/环境镜面（URP 的 `_SPECULARHIGHLIGHTS_OFF` 只关直射高光，
    `GlossyEnvironmentReflection` 仍然生效）→ 换 SimpleLit + 黑色 specular 才能做真正的哑光叶子
用法：python3 spike/tools/probe_black.py 0    # 归零
      python3 spike/tools/probe_black.py 1    # 还原（从 /tmp 备份）
"""
import os
import re
import shutil
import sys
import tempfile

MATS = ["spike/project/Assets/Settings/M_Bush.mat",
        "spike/project/Assets/Settings/M_BushLight.mat"]
BAK = os.path.join(tempfile.gettempdir(), "mistbell_probe_black")
os.makedirs(BAK, exist_ok=True)
mode = sys.argv[1] if len(sys.argv) > 1 else "0"

if mode == "0":
    for p in MATS:
        # 备份放 /tmp：放回 Assets/ 下会被 Unity 当成资产导入，留一堆无用的 .meta
        shutil.copy(p, os.path.join(BAK, os.path.basename(p)))
        s = open(p).read()
        s = re.sub(r"- _BaseColor: \{r: [^}]+\}", "- _BaseColor: {r: 0, g: 0, b: 0, a: 1}", s)
        s = re.sub(r"- _Color: \{r: [^}]+\}", "- _Color: {r: 0, g: 0, b: 0, a: 1}", s)
        open(p, "w").write(s)
        print("blackened", p)
else:
    for p in MATS:
        shutil.copy(os.path.join(BAK, os.path.basename(p)), p)
        print("restored", p)
