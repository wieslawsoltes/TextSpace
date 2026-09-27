#!/usr/bin/env python3
"""Fetch openly licensed fonts into the app. Records exact checksums for every build."""
from pathlib import Path
import base64
import concurrent.futures
import hashlib
import json
import os
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'src/TextSpace.App/Assets/Fonts'
DEST.mkdir(parents=True, exist_ok=True)
TOKEN = os.environ.get('GITHUB_TOKEN')
FILES = {'Inter.ttf': 'ofl/inter/Inter[opsz,wght].ttf', 'Inter-OFL.txt': 'ofl/inter/OFL.txt'}
for family in ('Carlito', 'Tinos', 'Cousine'):
    for style in ('Regular', 'Bold', 'Italic', 'BoldItalic'):
        FILES[f'{family}-{style}.ttf'] = f'ofl/{family.lower()}/{family}-{style}.ttf'
    FILES[f'{family}-OFL.txt'] = f'ofl/{family.lower()}/OFL.txt'

def get(path):
    headers = {'User-Agent': 'TextSpace-build'}
    url = 'https://raw.githubusercontent.com/google/fonts/main/' + urllib.parse.quote(path, safe='/')
    try:
        with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=90) as response:
            return response.read()
    except Exception:
        api = 'https://api.github.com/repos/google/fonts/contents/' + urllib.parse.quote(path, safe='/')
        if TOKEN: headers['Authorization'] = 'Bearer ' + TOKEN
        with urllib.request.urlopen(urllib.request.Request(api, headers=headers), timeout=90) as response:
            result = json.load(response)
        if result.get('encoding') == 'base64': return base64.b64decode(result['content'])
        with urllib.request.urlopen(result['download_url'], timeout=90) as response: return response.read()

def fetch(item):
    name, path = item
    output = DEST / name
    if output.exists() and output.stat().st_size > 1000:
        data = output.read_bytes()
    else:
        data = get(path)
        if len(data) < 1000: raise RuntimeError(f'Invalid asset: {name}')
        temporary = output.with_suffix(output.suffix + '.tmp'); temporary.write_bytes(data); temporary.replace(output)
    print(f'{name}: {len(data):,} bytes')
    return {'file': name, 'source': 'https://github.com/google/fonts/blob/main/' + path, 'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data)}

with concurrent.futures.ThreadPoolExecutor(max_workers=4) as executor:
    records = list(executor.map(fetch, FILES.items()))
(DEST / 'asset-manifest.json').write_text(json.dumps(records, indent=2) + '\n')
