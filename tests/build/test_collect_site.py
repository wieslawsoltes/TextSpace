import importlib.util
import contextlib
import io
import json
import sys
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


SCRIPT = Path(__file__).resolve().parents[2] / 'scripts' / 'collect-site.py'
spec = importlib.util.spec_from_file_location('collect_site', SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PublicationVersionTests(unittest.TestCase):
    def test_checked_out_package_version_is_used(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'Directory.Build.props').write_text(
                '<Project><PropertyGroup><Version>3.2.1-alpha.7</Version>'
                '</PropertyGroup></Project>')
            with patch.dict(os.environ, {'VERSION': ''}):
                self.assertEqual('3.2.1-alpha.7', module.resolve_version(root))

    def test_collected_metadata_uses_source_version_and_commit(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            publication = root / 'publish'
            (publication / '_framework').mkdir(parents=True)
            (publication / 'index.html').write_text('<html></html>')
            (publication / '_framework' / 'dotnet.js').write_text('export {};')
            (publication / '_framework' / 'runtime.wasm').write_bytes(b'\x00asm\x01\x00\x00\x00')
            target = root / 'site'
            with patch.dict(os.environ, {'VERSION': '', 'GITHUB_SHA': 'test-commit'}), \
                    patch.object(sys, 'argv', ['collect-site.py', str(publication), str(target)]), \
                    contextlib.redirect_stdout(io.StringIO()):
                module.main()
                expected = module.resolve_version()
            metadata = json.loads((target / 'build-info.json').read_text())
            self.assertEqual(expected, metadata['version'])
            self.assertEqual('test-commit', metadata['commit'])
            self.assertTrue((target / '.nojekyll').is_file())

    def test_explicit_release_override_is_respected(self):
        with tempfile.TemporaryDirectory() as directory:
            self.assertEqual('7.2.1', module.resolve_version(directory, '7.2.1'))

    def test_invalid_or_missing_versions_cannot_be_published(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'Directory.Build.props').write_text('<Project/>')
            for value in ('', 'main', '1.2', '1.2.3;exit', '$(Version)'):
                with self.subTest(value=value), patch.dict(os.environ, {'VERSION': ''}):
                    with self.assertRaises(ValueError):
                        module.resolve_version(root, value)


if __name__ == '__main__':
    unittest.main()
