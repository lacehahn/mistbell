#!/usr/bin/env python3
"""Real player save/load/settings and unchanged demo/capture regression gates."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'spike/project'
LOGS = ROOT / 'spike/logs'
APP = PROJECT / 'Builds/Mac/Mistbell.app/Contents/MacOS/Mistbell'
PERSIST = Path.home() / 'Library/Application Support/Lot/Mistbell'


def demo(name, *args):
    log = LOGS / f'save_runtime_{name}.log'
    command = [str(APP), '-force-gfx-direct', *args, '-screen-width', '1920',
               '-screen-height', '1080', '-screen-fullscreen', '0', '-logFile', str(log)]
    with (LOGS / f'save_stdout_{name}.log').open('w') as out:
        result = subprocess.run(command, cwd=PROJECT, stdout=out, stderr=out, timeout=180)
    print(f'[EXIT] {name}={result.returncode}', flush=True)
    text = log.read_text(errors='replace')
    for line in text.splitlines():
        if line.startswith(('[SAVE', '[SETTINGS]', '[POI]', '[PLAY]', '[MOVE]', '[REACH]', '[CINEMA]', '[REVEAL]', '[INTERIOR_U]')):
            print(line, flush=True)
    assert result.returncode == 0, name
    assert not re.search(r'(Exception:|NullReferenceException|error CS\d)', text), name
    return text


def main():
    LOGS.mkdir(exist_ok=True)
    PERSIST.mkdir(parents=True, exist_ok=True)
    files = [PERSIST / name for name in ('mistbell_save.json', 'mistbell_settings.json')]
    backups = {p: p.read_bytes() if p.exists() else None for p in files}
    try:
        for p in files:
            p.unlink(missing_ok=True)
        save_log = demo('save', '-demo-save')
        saved = json.loads(files[0].read_text())
        assert set(saved) == {'position', 'rotationY', 'insideInterior', 'doorOpen', 'visitedPois', 'version', 'savedAtUnix', 'visitCounts'}
        assert saved['version'] == 1 and saved['insideInterior'] and saved['doorOpen']
        assert saved['visitedPois'] == ['gergeti_p04']
        assert '[SAVE_DEMO] pass=true' in save_log
        load = demo('load', '-demo-load')
        matches = re.findall(r'\[SAVE\] load pos=.*? pois=(\d+) delta_m=([\d.]+)', load)
        assert matches and all(int(n) == len(saved['visitedPois']) and float(d) <= .05 for n, d in matches)
        settings = demo('settings', '-demo-settings')
        languages = re.findall(r'\[POI\] body_lang=(cn|en|both)', settings)
        transitions = [lang for i, lang in enumerate(languages) if i == 0 or languages[i-1] != lang]
        assert transitions[-3:] == ['cn', 'en', 'both'], languages
        stored = json.loads(files[1].read_text())
        assert abs(stored['volume'] - .8) < .0001 and stored['language'] == 'both'
        for name, args in [('walk', ['-demo-walk', '6']), ('stairs', ['-demo-walk-stairs']),
                           ('tower', ['-demo-walk-to-tower']), ('cinema', ['-demo-cinema']),
                           ('reveal', ['-demo-reveal']), ('enter', ['-demo-enter-church']),
                           ('poi', ['-demo-poi', 'gergeti_p04'])]:
            demo(name, *args)
        result = subprocess.run(['bash', str(ROOT / 'spike/tools/capture_runtime.sh')], cwd=ROOT,
                                env={**os.environ, 'MISTBELL_GFX_DIRECT': '1'}, timeout=120)
        print(f'[EXIT] capture_runtime.sh={result.returncode}', flush=True)
        assert result.returncode == 0
        names = ['save', 'load', 'settings_cn', 'settings_en', 'settings_both', 'settings_menu',
                 'interior_entry', 'interior_inside', 'interior_poi_p04',
                 'reveal_20', 'reveal_50', 'reveal_80', 'art_runtime', 'art_close', 'art_ground']
        for name in names:
            path = PROJECT / 'Shots' / (name + '.png')
            assert path.is_file() and path.stat().st_size > 0, path
            print(f'[FRAME] file={path.relative_to(ROOT)} md5={hashlib.md5(path.read_bytes()).hexdigest()}', flush=True)
            measure = subprocess.run([sys.executable, str(ROOT / 'spike/tools/measure_shot.py'), str(path)],
                                     capture_output=True, text=True, check=True)
            print(measure.stdout.strip(), flush=True)
            assert 'magenta_px=0 ' in measure.stdout, name
        # Verify rendered text, including unchanged title meshes after atlas repacks.
        from measure_shot import load
        for lang in ('cn', 'en', 'both'):
            pixels, width, height = load(str(PROJECT / 'Shots' / f'settings_{lang}.png'))
            assert (width, height) == (1920, 1080)
            def bright(x0, y0, x1, y1):
                return sum(min(pixels[(y * width + x) * 3:(y * width + x) * 3 + 3]) > 150
                           for y in range(y0, y1) for x in range(x0, x1))
            title = bright(630, 270, 1280, 320)
            cn = bright(630, 370, 1280, 535)
            en = bright(630, 565, 1280, 725)
            print(f'[LANG_PIXELS] lang={lang} title={title} cn={cn} en={en}', flush=True)
            assert title > 100
            assert (cn > 1000) if lang != 'en' else (cn < 20)
            assert (en > 1000) if lang != 'cn' else (en < 20)
        print('[VERIFY_SAVE_SETTINGS] ALL PASS', flush=True)
    finally:
        for p, data in backups.items():
            if data is None:
                p.unlink(missing_ok=True)
            else:
                p.write_bytes(data)


if __name__ == '__main__':
    main()
