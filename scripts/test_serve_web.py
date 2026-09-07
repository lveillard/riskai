import functools
import gzip
import http.server
from pathlib import Path
import tempfile
import threading
import unittest
import urllib.error
import urllib.request

from serve_web import UnityHandler


class QuietHandler(UnityHandler):
    def log_message(self, *_args):
        pass


class ServeWebTests(unittest.TestCase):
    def test_compressed_wasm_has_correct_mime_and_encoding(self):
        with tempfile.TemporaryDirectory() as directory:
            payload = b'\x00asmtest'
            Path(directory, 'game.wasm.unityweb').write_bytes(gzip.compress(payload))
            handler = functools.partial(QuietHandler, directory=directory)
            server = http.server.ThreadingHTTPServer(('127.0.0.1', 0), handler)
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            try:
                url = f'http://127.0.0.1:{server.server_port}/game.wasm.unityweb'
                with urllib.request.urlopen(url) as response:
                    self.assertEqual(response.headers['Content-Type'], 'application/wasm')
                    self.assertEqual(response.headers['Content-Encoding'], 'gzip')
                    self.assertEqual(gzip.decompress(response.read()), payload)
                with self.assertRaises(urllib.error.HTTPError) as missing:
                    urllib.request.urlopen(url + '-missing')
                self.assertEqual(missing.exception.code, 404)
                self.assertIsNone(missing.exception.headers.get('Content-Encoding'))
                missing.exception.close()
            finally:
                server.shutdown()
                thread.join()
                server.server_close()


if __name__ == '__main__':
    unittest.main()
