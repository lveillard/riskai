"""Serve an exported Unity player locally, with native gzip delivery and WASM MIME.

Use --bind 0.0.0.0 explicitly to test from a tablet on the same LAN.
"""
import argparse
import functools
import http.server
from pathlib import Path


class UnityHandler(http.server.SimpleHTTPRequestHandler):
    def guess_type(self, path):
        normalized = str(path)
        if normalized.endswith('.unityweb'):
            normalized = normalized[:-len('.unityweb')]
        if normalized.endswith('.wasm'):
            return 'application/wasm'
        if normalized.endswith('.js'):
            return 'text/javascript'
        return super().guess_type(normalized)

    def end_headers(self):
        path = Path(self.translate_path(self.path))
        if path.is_file() and path.suffix == '.unityweb':
            with path.open('rb') as stream:
                if stream.read(2) == b'\x1f\x8b':
                    self.send_header('Content-Encoding', 'gzip')
        self.send_header('Cache-Control', 'no-cache')
        super().end_headers()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, default=Path(__file__).resolve().parents[1] / 'Builds' / 'Web-v0.21')
    parser.add_argument('--bind', default='127.0.0.1')
    parser.add_argument('--port', type=int, default=8080)
    args = parser.parse_args()
    directory = args.directory.resolve()
    if not (directory / 'index.html').is_file():
        parser.error(f'No Web build found at {directory}. Run scripts/Unity.ps1 -Action BuildWeb first.')
    handler = functools.partial(UnityHandler, directory=str(directory))
    with http.server.ThreadingHTTPServer((args.bind, args.port), handler) as server:
        print(f'RiskAI: http://{args.bind}:{args.port}', flush=True)
        server.serve_forever()


if __name__ == '__main__':
    main()
