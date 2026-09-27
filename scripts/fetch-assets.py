#!/usr/bin/env python3
"""Acquire pinned open font assets with atomic writes and per-file provenance."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'src/TextSpace.App/Assets/Fonts'
GOOGLE_REV = '23e54b51ddffbc7713c583748e3bd86f62b1fa4a'
BASE = f'https://raw.githubusercontent.com/google/fonts/{GOOGLE_REV}/'
FILES = {'Inter.ttf': BASE + 'ofl/inter/Inter%5Bopsz,wght%5D.ttf', 'Inter-OFL.txt': BASE + 'ofl/inter/OFL.txt'}
for family in ('Carlito', 'Tinos', 'Cousine'):
    for style in ('Regular', 'Bold', 'Italic', 'BoldItalic'):
        FILES[f'{family}-{style}.ttf'] = BASE + f'ofl/{family.lower()}/{family}-{style}.ttf'
    FILES[f'{family}-OFL.txt'] = BASE + f'ofl/{family.lower()}/OFL.txt'
# Google Fonts' pinned Tinos directory omits its license. Its METADATA.pb names
# this exact upstream revision; use that revision's original license verbatim.
FILES['Tinos-OFL.txt'] = 'https://raw.githubusercontent.com/googlefonts/tinos/3b4482a99b80ea5fc75f187b1be3120a3f5905b3/OFL.txt'

def fetch(item):
    name, url = item
    destination = DEST / name
    stamp = destination.with_suffix(destination.suffix + '.source')
    if destination.exists() and stamp.exists() and stamp.read_text() == url:
        data = destination.read_bytes()
    else:
        for attempt in range(3):
            try:
                request = urllib.request.Request(url, headers={'User-Agent': 'TextSpace-build'})
                with urllib.request.urlopen(request, timeout=45) as response:
                    data = response.read(8 * 1024 * 1024 + 1)
                break
            except (OSError, urllib.error.URLError):
                if attempt == 2:
                    raise
                time.sleep(attempt + 1)
        if not 1000 <= len(data) <= 8 * 1024 * 1024:
            raise RuntimeError(f'Invalid asset size for {name}')
        if name.endswith('.ttf') and data[:4] not in (b'\x00\x01\x00\x00', b'OTTO', b'true'):
            raise RuntimeError(f'Invalid font signature for {name}')
        temporary = destination.with_suffix(destination.suffix + '.tmp')
        temporary.write_bytes(data)
        temporary.replace(destination)
        stamp.write_text(url)
    print(f'{name}: {len(data):,} bytes', flush=True)
    return {'file': name, 'source': url, 'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data)}

def main():
    DEST.mkdir(parents=True, exist_ok=True)
    with ThreadPoolExecutor(max_workers=4) as executor:
        records = list(executor.map(fetch, FILES.items()))
    (DEST / 'asset-manifest.json').write_text(json.dumps(records, indent=2) + '\n')

if __name__ == '__main__':
    main()
