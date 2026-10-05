"""Exercise the actual Linux deployment script against temporary releases."""
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tarfile
import tempfile
import unittest

from deploy_azure_vm import REMOTE


@unittest.skipUnless(os.name == 'posix', 'The production deployment runs on Linux')
class DeploymentTests(unittest.TestCase):
    def test_shared_assets_preserve_rollback_and_reject_invalid_activation(self):
        with tempfile.TemporaryDirectory(prefix='riskai-deploy-test-') as folder:
            root = Path(folder)
            previous = root / 'releases' / 'old'
            previous.mkdir(parents=True)
            current = root / 'current'
            current.symlink_to(previous, target_is_directory=True)
            (previous / 'index.html').write_bytes(b'old index')
            (previous / 'shared.bundle').write_bytes(b'immutable asset')
            (previous / 'replace.txt').write_bytes(b'old bytes')
            files = {'index.html': b'new index', 'release-test/new.bundle': b'new asset', 'replace.txt': b'new bytes'}
            archive = root / 'release.tar'
            with tarfile.open(archive, 'w') as bundle:
                for name, data in files.items():
                    item = tarfile.TarInfo(name)
                    item.size = len(data)
                    bundle.addfile(item, io.BytesIO(data))
            config = {'current': str(current), 'expected_previous': str(previous), 'release_id': 'test-release',
                      'archive': str(archive), 'archive_sha256': hashlib.sha256(archive.read_bytes()).hexdigest(),
                      'manifest': {name: hashlib.sha256(data).hexdigest() for name, data in files.items()}}

            def deploy():
                return subprocess.run([sys.executable, '-c', REMOTE, json.dumps(config)], capture_output=True, text=True)

            result = deploy()
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertTrue(json.loads(result.stdout)['activated'])
            target = current.resolve()
            self.assertEqual((target / 'shared.bundle').stat().st_ino, (previous / 'shared.bundle').stat().st_ino)
            self.assertEqual((previous / 'index.html').read_bytes(), b'old index')
            self.assertEqual((previous / 'replace.txt').read_bytes(), b'old bytes')
            self.assertEqual((target / 'replace.txt').read_bytes(), b'new bytes')
            self.assertNotEqual((target / 'index.html').stat().st_ino, (previous / 'index.html').stat().st_ino)
            self.assertNotEqual(deploy().returncode, 0, 'A changed current release must reject stale deployment state')
            config.update(expected_previous=str(target), release_id='tampered', archive_sha256='0' * 64)
            self.assertNotEqual(deploy().returncode, 0, 'A corrupt archive must not be activated')
            self.assertEqual(current.resolve(), target)
            self.assertFalse((root / 'releases' / 'tampered').exists())


if __name__ == '__main__':
    unittest.main()
