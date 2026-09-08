"""Prepare a versioned Unity release; --execute uploads and activates it on nginx VM.

The existing current symlink and all previous assets are preserved. No nginx edits.
Use only after validating the build. Default operation is entirely local.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import shlex
import subprocess
import tarfile


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def prepare(build, output, release_id):
    prefix = f'release-{release_id}'
    index = (build / 'index.html').read_text(encoding='utf-8')
    for before, after, expected in [
        ('Build/', f'{prefix}/Build/', 4),
        ('StreamingAssets', f'{prefix}/StreamingAssets', 1),
        ('riskai-pen.js', f'{prefix}/riskai-pen.js', 1),
    ]:
        if index.count(before) != expected:
            raise ValueError(f'Unexpected index template: expected {expected} occurrences of {before!r}')
        index = index.replace(before, after)
    for required in ('Build', 'riskai-pen.js'):
        if not (build / required).exists():
            raise ValueError(f'Missing build component: {required}')
    files = sorted(build.rglob('*'))
    if any(p.is_symlink() for p in files):
        raise ValueError('Build must not contain symlinks')
    manifest = {}
    archive = output / 'release.tar'
    with tarfile.open(archive, 'w') as bundle:
        for path in files:
            if not path.is_file():
                continue
            if path.suffix == '.unityweb':
                with path.open('rb') as stream:
                    if stream.read(2) != b'\x1f\x8b':
                        raise ValueError(f'Existing nginx requires gzip: {path}')
            name = f'{prefix}/{path.relative_to(build).as_posix()}'
            manifest[name] = digest(path)
            bundle.add(path, arcname=name, recursive=False)
        raw = index.encode('utf-8')
        info = tarfile.TarInfo('index.html')
        info.size, info.mode = len(raw), 0o644
        bundle.addfile(info, io.BytesIO(raw))
        manifest['index.html'] = hashlib.sha256(raw).hexdigest()
    (output / 'index.html').write_text(index, encoding='utf-8')
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    return archive, manifest


REMOTE = r'''
import hashlib, json, os, pathlib, shutil, subprocess, sys, tarfile
c = json.loads(sys.argv[1])
current = pathlib.Path(c['current'])
releases = current.parent / 'releases'
assert current.is_symlink(), 'Current must already be a symlink'
previous = current.resolve(strict=True)
assert previous.parent == releases.resolve(strict=True), 'Unexpected current release location'
assert str(previous) == c['expected_previous'], 'Current changed since preflight'
target = releases / c['release_id']
assert not target.exists(), 'Release already exists; choose a new release id'
next_link = current.parent / ('current-next-' + c['release_id'])
assert not os.path.lexists(next_link), 'Temporary symlink already exists'
archive = pathlib.Path(c['archive'])
with archive.open('rb') as f:
    assert hashlib.file_digest(f, 'sha256').hexdigest() == c['archive_sha256'], 'Upload hash mismatch'
# Keep previous URLs available for clients with an older index or cached loader.
subprocess.run(['cp', '-a', str(previous), str(target)], check=True)
with tarfile.open(archive) as bundle:
    members = bundle.getmembers()
    assert len(members) == len(c['manifest']), 'Unexpected archive members'
    assert {m.name for m in members} == set(c['manifest']), 'Archive manifest mismatch'
    for member in sorted(members, key=lambda m: m.name == 'index.html'):
        rel = pathlib.PurePosixPath(member.name)
        assert member.isfile() and not rel.is_absolute() and '..' not in rel.parts
        destination = target / rel
        assert destination.resolve().is_relative_to(target.resolve()), 'Unsafe inherited symlink'
        destination.parent.mkdir(parents=True, exist_ok=True)
        # Replace files, never truncate an inherited hardlink or follow a symlink.
        temporary = destination.with_name(destination.name + '.deploy-new')
        with bundle.extractfile(member) as source, temporary.open('xb') as out:
            shutil.copyfileobj(source, out)
        temporary.chmod(0o644)
        with temporary.open('rb') as f:
            assert hashlib.file_digest(f, 'sha256').hexdigest() == c['manifest'][member.name], 'File hash mismatch'
        os.replace(temporary, destination)
# Abort if another operator changed production during staging.
assert current.resolve(strict=True) == previous, 'Current changed during deployment'
os.symlink(target, next_link)
os.replace(next_link, current)
print(json.dumps({'previous': str(previous), 'current': str(target), 'files_verified': len(c['manifest']), 'activated': True}))
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True, help='New local receipt/bundle directory')
    parser.add_argument('--release-id', required=True, help='Unique UTC timestamp, optionally with commit suffix')
    parser.add_argument('--host', required=True, help='SSH destination, e.g. azureuser@68.221.185.54')
    parser.add_argument('--key', type=Path, required=True)
    parser.add_argument('--known-hosts', type=Path, required=True)
    parser.add_argument('--current', default='/srv/riskai/current')
    parser.add_argument('--execute', action='store_true', help='Upload, verify and atomically activate; default only prepares locally')
    args = parser.parse_args()
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_-]{5,79}', args.release_id):
        parser.error('Release id must be 6–80 letters, digits, underscores or hyphens')
    if not re.fullmatch(r'[a-z_][a-z0-9_-]*@[A-Za-z0-9][A-Za-z0-9.-]*', args.host):
        parser.error('Host must be user@hostname or user@IPv4')
    current = PurePosixPath(args.current)
    if not re.fullmatch(r'/[A-Za-z0-9_/-]+', args.current) or '..' in current.parts or current.name != 'current' or len(current.parts) < 4:
        parser.error('Current must be an absolute project path ending in /current')
    build = args.build.resolve(strict=True)
    output = args.output.resolve()
    if output.is_relative_to(build):
        parser.error('Output must be outside the build')
    key, known_hosts = args.key.resolve(strict=True), args.known_hosts.resolve(strict=True)
    output.mkdir(parents=True, exist_ok=False)
    archive, manifest = prepare(build, output, args.release_id)
    options = ['-i', str(key), '-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes', '-o', f'UserKnownHostsFile={known_hosts}', '-o', 'ConnectTimeout=20']
    ssh = ['ssh', *options, args.host]
    config = {'current': str(current), 'release_id': args.release_id, 'archive_sha256': digest(archive), 'manifest': manifest}
    receipt = {'host': args.host, 'build': str(build), 'config': config, 'activated': False}
    receipt_path = output / 'receipt.json'
    receipt_path.write_text(json.dumps(receipt, indent=2) + '\n')
    print(f'Prepared {archive}; {len(manifest)} files; SHA256 {config["archive_sha256"]}', flush=True)
    if not args.execute:
        print('Local preparation only. Run again with a new output directory and --execute to publish.')
        return
    previous = subprocess.check_output([*ssh, f'readlink -f {shlex.quote(str(current))}'], text=True).strip()
    if PurePosixPath(previous).parent != current.parent / 'releases':
        raise ValueError('Unexpected previous release path')
    rollback_link = str(current.parent / f'rollback-{args.release_id}')
    rollback = f'sudo -n ln -s {shlex.quote(previous)} {shlex.quote(rollback_link)} && sudo -n mv -Tf {shlex.quote(rollback_link)} {shlex.quote(str(current))}'
    receipt['previous'] = previous
    config['expected_previous'] = previous
    receipt['rollback_ssh_argv'] = [*ssh, rollback]
    receipt_path.write_text(json.dumps(receipt, indent=2) + '\n')
    remote_archive = subprocess.check_output([*ssh, 'mktemp /tmp/riskai-upload-XXXXXXXX.tar'], text=True).strip()
    if not re.fullmatch(r'/tmp/riskai-upload-[A-Za-z0-9]+\.tar', remote_archive):
        raise ValueError('Unexpected remote temporary archive path')
    config['archive'] = remote_archive
    try:
        subprocess.run(['scp', *options, str(archive), f'{args.host}:{remote_archive}'], check=True)
        command = 'sudo -n python3 - ' + shlex.quote(json.dumps(config))
        result = subprocess.run([*ssh, command], input=REMOTE, text=True, capture_output=True)
        if result.returncode:
            raise RuntimeError(f'Remote deployment failed; consult current symlink before retrying.\n{result.stderr}')
        receipt.update(json.loads(result.stdout))
        receipt_path.write_text(json.dumps(receipt, indent=2) + '\n')
        print(json.dumps(receipt, indent=2))
    finally:
        subprocess.run([*ssh, 'rm -f -- ' + shlex.quote(remote_archive)], check=False)


if __name__ == '__main__':
    main()
