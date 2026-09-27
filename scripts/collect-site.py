#!/usr/bin/env python3
"""Collect the real Uno WebAssembly static root, retaining its generated bootstrap assets."""
import argparse,json,os,shutil
from pathlib import Path
p=argparse.ArgumentParser(); p.add_argument('publish',type=Path); p.add_argument('output',type=Path); a=p.parse_args()
roots=sorted(a.publish.rglob('index.html'),key=lambda f:len(f.parts))
if not roots: raise SystemExit('Uno publication contains no index.html')
source=roots[0].parent
if not any(source.rglob('*.wasm')): raise SystemExit('Publication has no WebAssembly binaries')
shutil.copytree(source,a.output,dirs_exist_ok=True)
(a.output/'.nojekyll').touch()
(a.output/'build-info.json').write_text(json.dumps({'application':'TextSpace','host':'Uno WebAssembly','commit':os.environ.get('GITHUB_SHA','local'),'version':os.environ.get('VERSION','0.1.0-alpha.1')},indent=2)+'\n')
print('Collected',source,'to',a.output)
