#!/usr/bin/env python3
"""Collect and validate the real Uno publication before it can reach Pages."""
import argparse
import json
import os
import re
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path


def resolve_version(root=None, override=None):
    """Read the package version instead of leaving stale release literals."""
    root = Path(root) if root is not None else Path(__file__).resolve().parents[1]
    if override is None:
        override = os.environ.get('VERSION')
    version = override
    if not version:
        properties = ET.parse(root / 'Directory.Build.props').getroot()
        version = properties.findtext('./PropertyGroup/Version')
    version = (version or '').strip()
    if not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?', version):
        raise ValueError('A valid package version is required for publication provenance')
    return version


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('publish', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    version = resolve_version()
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
        'version': version
    }, indent=2) + '\n')
    print('Collected', source, 'to', args.output, flush=True)


if __name__ == '__main__':
    main()
