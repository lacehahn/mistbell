#!/usr/bin/env python3
"""Horizon task gate for the locked 1920x1080 art_runtime camera.

Sky ROI: every column, rows 0..161 (above all mountain silhouettes).
Horizon ROI: both exposed snow silhouettes, x=19..383 and 1728..1900.
Rows come from collider raycasts exported by HorizonHazeSky.Run/Diagnose;
measure raw adjacent vertical pixels within +/-2 pixels of that contour.
No blur, percentile clipping, or removal of snow particles.
Other shots print FRAME and the unchanged measure_shot.py output only.
"""
import argparse
import csv
import hashlib
from pathlib import Path
import re
import subprocess
import sys

from measure_shot import load


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("shots", nargs="+", type=Path)
    parser.add_argument("--contour", type=Path, default=Path("spike/logs/horizon_contour.csv"))
    parser.add_argument("--setup-log", type=Path, default=Path("spike/logs/horizon_build.log"))
    args = parser.parse_args()
    for path in args.shots:
        print(f"[FRAME] file={path} md5={hashlib.md5(path.read_bytes()).hexdigest()}", flush=True)
        subprocess.run([sys.executable, str(Path(__file__).with_name("measure_shot.py")), str(path)], check=True)

    buf, w, h = load(str(args.shots[0]))
    if (w, h) != (1920, 1080):
        raise ValueError("Use the locked 1920x1080 art_runtime capture as the first shot")
    def luma(x, y):
        i=(y*w+x)*3
        return (.2126*buf[i]+.7152*buf[i+1]+.0722*buf[i+2])/255

    sky_rows=162
    columns=[sum(luma(x,y) for y in range(sky_rows))/sky_rows for x in range(w)]
    stripe=max(abs(a-b) for a,b in zip(columns,columns[1:]))
    band_bad=band_total=0
    for y in range(sky_rows):
        for x in range(w):
            v=luma(x,y)
            if x+1<w:
                band_bad+=abs(v-luma(x+1,y))>.02
                band_total+=1
            if y+1<sky_rows:
                band_bad+=abs(v-luma(x,y+1))>.02
                band_total+=1
    band=band_bad/band_total
    horizon=0
    with args.contour.open() as f:
        contour=list(csv.DictReader(f))
    if len(contour)!=538:
        raise ValueError("Expected all 538 exposed contour columns")
    for point in contour:
        x=int(point["x"]); y=int(float(point["y"]))
        horizon=max(horizon,max(abs(luma(x,j+1)-luma(x,j)) for j in range(y-2,y+3)))

    setup=re.findall(r"\[HORIZON_SETUP\] (.*)",args.setup_log.read_text())[-1]
    values=dict(re.findall(r"(\w+)=([^ ]+)", setup))
    print(f"[HORIZON] haze_far={values['haze_far']} haze_color={values['haze_color']} "
          f"sky_steps={values['sky_steps']} dither={values['dither']} cloud_scale={values['cloud_scale']} "
          f"stripe_max_dev={stripe:.6f} horizon_step_max={horizon:.6f} sky_band_pct={band:.6f}", flush=True)
    import json
    measured=json.loads(subprocess.check_output([sys.executable,str(Path(__file__).with_name("measure_shot.py")),str(args.shots[0]),"--json"]))
    gates={"stripe":stripe<=.03,"horizon":horizon<=.035,"sky_band":band<=.05,
           "magenta":measured["magenta_px"]==0,"snow":measured["snow_ratio"]>=.55,
           "white_clip":measured["white_clip_ratio"]<=.05}
    print("[HORIZON_GATE] "+" ".join(f"{key}={'pass' if ok else 'FAIL'}" for key,ok in gates.items()),flush=True)
    return 0 if all(gates.values()) else 1


if __name__ == "__main__":
    sys.exit(main())
