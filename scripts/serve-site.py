#!/usr/bin/env python3
"""Serve an immutable Pages-shaped publication; never rewrite missing assets to HTML."""
import argparse
import functools
import hashlib
import http.server
import json
from pathlib import Path
from urllib.parse import unquote, urlsplit


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('directory', type=Path)
    parser.add_argument('--port', type=int, default=4173)
    parser.add_argument('--base', default='/TextSpace/')
    parser.add_argument('--ready-file', type=Path)
    args = parser.parse_args()
    root = args.directory.resolve(strict=True)
    base = '/' + args.base.strip('/') + '/'
    if not (root / 'index.html').is_file():
        raise SystemExit(f'{root} is not a static application root')

    class Handler(http.server.SimpleHTTPRequestHandler):
        extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map,
                          '.wasm': 'application/wasm', '.js': 'text/javascript',
                          '.mjs': 'text/javascript', '.json': 'application/json'}

        def translate_path(self, path):
            decoded = unquote(urlsplit(path).path)
            if not decoded.startswith(base):
                return str(root / '__invalid_base_path__')
            relative = decoded[len(base):]
            candidate = (root / relative).resolve()
            if candidate != root and root not in candidate.parents:
                return str(root / '__invalid_asset_path__')
            return str(candidate)

        def end_headers(self):
            self.send_header('Cache-Control', 'no-store')
            self.send_header('X-TextSpace-Static-Root', 'verified-publication')
            self.send_header('X-Content-Type-Options', 'nosniff')
            super().end_headers()

        def list_directory(self, path):
            self.send_error(404, 'Directory listing is disabled')
            return None

    server = http.server.ThreadingHTTPServer(('127.0.0.1', args.port),
        functools.partial(Handler, directory=str(root)))
    url = f'http://127.0.0.1:{server.server_port}{base}'
    metadata = {'url': url, 'root': str(root),
                'indexSha256': hashlib.sha256((root / 'index.html').read_bytes()).hexdigest()}
    print(json.dumps(metadata), flush=True)
    if args.ready_file:
        args.ready_file.parent.mkdir(parents=True, exist_ok=True)
        temporary = args.ready_file.with_suffix('.tmp')
        temporary.write_text(json.dumps(metadata))
        temporary.replace(args.ready_file)
    try:
        server.serve_forever()
    finally:
        server.server_close()


if __name__ == '__main__':
    main()
