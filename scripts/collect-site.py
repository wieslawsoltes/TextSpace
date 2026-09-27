#!/usr/bin/env python3
"""Collect and validate the real Uno publication before it can reach Pages."""
import argparse
import json
import os
import re
import shutil
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('publish', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    roots = sorted(args.publish.rglob('index.html'), key=lambda p: len(p.parts))
    if not roots:
        raise SystemExit('Uno publication contains no index.html')
    source = roots[0].parent
    print('Publication roots:', *roots, sep='\n', flush=True)
    print('Published runtime assets:', *args.publish.rglob('dotnet*'), sep='\n', flush=True)
    for config in source.rglob('uno-config.js'):
        print('Runtime configuration:', config, flush=True)
        for line in config.read_text().splitlines():
            if any(word in line.lower() for word in ('dotnet', 'base', 'framework', 'runtime')):
                print(line, flush=True)
    if not any(source.rglob('*.wasm')):
        raise SystemExit('Publication has no WebAssembly binaries')
    framework = source / '_framework'
    entries = list(framework.glob('dotnet.js')) + [p for p in framework.glob('dotnet.*.js') if re.fullmatch(r'dotnet\.[a-z0-9]+\.js', p.name)]
    if not entries:
        print('Build output runtime assets:', *Path('src/TextSpace.App/bin').rglob('dotnet*'), sep='\n', flush=True)
        raise SystemExit('Publication is missing its _framework/dotnet runtime entry module; refusing to deploy an unbootable application')
    shutil.copytree(source, args.output, dirs_exist_ok=True)
    (args.output / '.nojekyll').touch()
    (args.output / 'build-info.json').write_text(json.dumps({
        'application': 'TextSpace', 'host': 'Uno WebAssembly',
        'commit': os.environ.get('GITHUB_SHA', 'local'),
        'version': os.environ.get('VERSION', '0.3.0-alpha.1')
    }, indent=2) + '\n')
    print('Collected', source, 'to', args.output, flush=True)


if __name__ == '__main__':
    main()
