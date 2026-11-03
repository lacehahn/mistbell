#!/usr/bin/env python3
"""Exercise journal in the built player, preserving the player's progress/settings."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
from verify_save_settings import ROOT, PROJECT, LOGS, PERSIST, demo


def main():
    LOGS.mkdir(exist_ok=True)
    PERSIST.mkdir(parents=True, exist_ok=True)
    files = [PERSIST / n for n in ('mistbell_save.json', 'mistbell_settings.json')]
    backups = {p: p.read_bytes() if p.exists() else None for p in files}
    try:
        for p in files:
            p.unlink(missing_ok=True)
        text = demo('journal', '-demo-journal')
        for line in text.splitlines():
            if line.startswith('[JOURNAL'):
                print(line, flush=True)
        assert '[JOURNAL] total=8 visited=1 unread=7 visit_counts=1 label_unread=true menu_entry=true' in text
        assert '[JOURNAL_CHECK] read_ugui=true p04_count=3 pass=true' in text
        saved = json.loads(files[0].read_text())
        assert saved['visitCounts'] == {'gergeti_p04': 3}
        # Legacy saves migrate without losing already-read notes.
        saved.pop('visitCounts')
        files[0].write_text(json.dumps(saved))
        demo('legacy_load', '-demo-load')
        demo('save', '-demo-save')
        assert json.loads(files[0].read_text())['visitCounts'] == {'gergeti_p04': 1}
        demo('load', '-demo-load')
        demo('settings', '-demo-settings')
        demo('enter', '-demo-enter-church')
        demo('walk', '-demo-walk', '6')
        result = subprocess.run(['bash', str(ROOT / 'spike/tools/capture_runtime.sh')],
                                cwd=ROOT, env={**os.environ, 'MISTBELL_GFX_DIRECT': '1'}, timeout=300)
        print(f'[EXIT] capture_runtime.sh={result.returncode}', flush=True)
        assert result.returncode == 0
        names = ['journal_list', 'poi_p04_read', 'save', 'load', 'settings_cn', 'settings_en',
                 'settings_both', 'settings_menu', 'interior_entry', 'interior_inside',
                 'art_runtime', 'art_close', 'art_ground']
        for name in names:
            path = PROJECT / 'Shots' / (name + '.png')
            assert path.is_file() and path.stat().st_size > 0
            print(f'[FRAME] file={path.relative_to(ROOT)} md5={hashlib.md5(path.read_bytes()).hexdigest()}', flush=True)
            measured = subprocess.run([sys.executable, str(ROOT / 'spike/tools/measure_shot.py'), str(path)],
                                      capture_output=True, text=True)
            print(measured.stdout.strip(), flush=True)
            print(f'[EXIT] measure_shot.py:{name}={measured.returncode}', flush=True)
            assert measured.returncode == 0 and 'magenta_px=0 ' in measured.stdout
        print('[VERIFY_JOURNAL] ALL PASS', flush=True)
    finally:
        for p, content in backups.items():
            if content is None:
                p.unlink(missing_ok=True)
            else:
                p.write_bytes(content)


if __name__ == '__main__':
    main()
