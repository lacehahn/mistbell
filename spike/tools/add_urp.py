#!/usr/bin/env python3
"""把 URP 加进 manifest（版本与编辑器 6000.3.27f2 匹配：17.0.1）。"""
import json
import os

p = os.path.expanduser("~/workspace/mistbell/spike/project/Packages/manifest.json")
data = json.load(open(p))
deps = data.setdefault("dependencies", {})
added = []

for pkg, ver in (
    ("com.unity.render-pipelines.universal", "17.0.1"),
    ("com.unity.shadergraph", "17.0.1"),
):
    if pkg not in deps:
        deps[pkg] = ver
        added.append((pkg, ver))

data["dependencies"] = dict(sorted(deps.items()))
with open(p, "w") as fh:
    json.dump(data, fh, indent=2)
    fh.write("\n")

print("added:", added or "none (already present)")
print("total packages:", len(data["dependencies"]))
