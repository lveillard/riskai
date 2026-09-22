"""Verify the public Unity release against the Azure deployment manifest."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
from urllib.request import Request, urlopen


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', required=True)
    parser.add_argument('--manifest', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    manifest = json.loads(args.manifest.read_text(encoding='utf-8'))
    report = {'url': args.url, 'files': [], 'success': True}
    for path, expected in manifest.items():
        row = {'path': path}
        try:
            request = Request(args.url.rstrip('/') + '/' + path,
                              headers={'Accept-Encoding': 'gzip', 'User-Agent': 'Riesgus-Release-Verification'})
            with urlopen(request, timeout=90) as response:
                row.update(status=response.status, encoding=response.headers.get('Content-Encoding'),
                           contentType=response.headers.get('Content-Type'),
                           cacheControl=response.headers.get('Cache-Control'))
                # Unity bundles are already gzip files in the manifest. Other
                # files may receive transport compression from the CDN.
                stream = gzip.GzipFile(fileobj=response) if row['encoding'] == 'gzip' and not path.endswith('.unityweb') else response
                digest = hashlib.sha256()
                for block in iter(lambda: stream.read(128 * 1024), b''):
                    digest.update(block)
                row['sha256'] = digest.hexdigest()
                row['matches'] = row['sha256'] == expected
        except Exception as error:
            row.update(matches=False, error=str(error))
        report['success'] = report['success'] and row['matches']
        report['files'].append(row)
        print(f"{'OK' if row['matches'] else 'FAILED'} {path}", flush=True)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return 0 if report['success'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
