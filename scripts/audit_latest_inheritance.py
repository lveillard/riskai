"""Inspect cached Blizzard TVFS and fetch only small balance tables; no game install.

Run from repository root: python scripts/audit_latest_inheritance.py
TVFS format follows CascLib src/CascRootFile_TVFS.cpp (cached beside inputs).
"""
from pathlib import Path
import hashlib, json, struct, urllib.request, zlib, re, sys

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / '.tools/v22-validation/reforged-latest'
DATA = ROOT / '.tools/cdn-current/2.0.4.23745'

def tvfs(b):
    assert b[:4] == b'TVFS'
    po, ps, vo, vs, co, cs = struct.unpack_from('>6I', b, 12)
    width = max(1, (cs.bit_length()+7)//8)
    files = {}
    def walk(p, end, prefix):
        name = prefix
        while p < end:
            if b[p] == 0: name += '/'; p += 1
            if b[p] != 255:
                n = b[p]; p += 1
                name += b[p:p+n].decode(); p += n
            if p < end and b[p] == 0: name += '/'; p += 1
            if p < end and b[p] == 255:
                node = int.from_bytes(b[p+1:p+5], 'big'); p += 5
                if node & 0x80000000:
                    stop = p+(node & 0x7fffffff)-4
                    walk(p, stop, name); p = stop
                else:
                    v = vo+node
                    assert b[v] == 1, 'Only single-span audit inputs supported'
                    size = int.from_bytes(b[v+5:v+9], 'big')
                    c = co+int.from_bytes(b[v+9:v+9+width], 'big')
                    files[name.strip('/')] = (b[c:c+b[6]].hex(), size)
                name = prefix
            elif p < end: name += '/'
    walk(po, po+ps, '')
    return files

def blte(b):
    assert b[:4] == b'BLTE'
    head = int.from_bytes(b[4:8], 'big')
    def chunk(c):
        if c[:1] == b'N': return c[1:]
        if c[:1] == b'Z': return zlib.decompress(c[1:])
        raise ValueError('Unsupported BLTE compression')
    if not head: return chunk(b[8:])
    count = int.from_bytes(b[9:12], 'big'); p = head; out = []
    for i in range(count):
        n, decoded = struct.unpack_from('>II', b, 12+24*i)
        c = b[p:p+n]; p += n
        assert hashlib.md5(c).digest() == b[20+24*i:36+24*i]
        d = chunk(c); assert len(d) == decoded
        out.append(d)
    return b''.join(out)

PROVENANCE = {}
VERIFY = '--verify-cdn' in sys.argv
def verify_encoded(encoded, ekey):
    header = int.from_bytes(encoded[4:8], 'big')
    assert hashlib.md5(encoded[:header] if header else encoded).hexdigest() == ekey

def fetch(key, size, output):
    verify = VERIFY and (output.suffix == '.tvfs' or output.name in ['unitbalance.slk','unitweapons.slk','unitdata.slk','unitmetadata.slk'])
    config = (CACHE / '.tools_9a94ff7d25781db6c09190cd69d52458.bin').read_text()
    full = next((k for k in re.findall(r'\b[0-9a-f]{32}\b', config) if k.startswith(key)), None)
    if full:
        url = 'https://us.cdn.blizzard.com/tpr/war3/data/'+full[:2]+'/'+full[2:4]+'/'+full
        if not output.exists() or verify:
            with urllib.request.urlopen(url, timeout=60) as response: encoded = response.read()
            verify_encoded(encoded, full)
            decoded = blte(encoded); assert len(decoded) == size
            if output.exists(): assert output.read_bytes() == decoded
            output.parent.mkdir(parents=True, exist_ok=True); output.write_bytes(decoded)
        assert output.stat().st_size == size
        PROVENANCE[str(output.relative_to(ROOT))] = dict(url=url, ekey=full, bytes=size, sha256=hashlib.sha256(output.read_bytes()).hexdigest(), remote_roundtrip_verified=verify)
        return output.read_bytes()
    for index in (ROOT / '.tools/cdn-current/indexes').glob('*.index'):
        b = index.read_bytes(); p = b.find(bytes.fromhex(key))
        if p < 0: continue
        fullkey = b[p:p+16].hex()
        n, offset = struct.unpack_from('>II', b, p+16)
        url = 'https://us.cdn.blizzard.com/tpr/war3/data/'+index.stem[:2]+'/'+index.stem[2:4]+'/'+index.stem
        if not output.exists() or verify:
            req = urllib.request.Request(url, headers={'Range':f'bytes={offset}-{offset+n-1}'})
            with urllib.request.urlopen(req, timeout=60) as response:
                assert response.status == 206
                encoded = response.read()
            assert len(encoded) == n
            verify_encoded(encoded, fullkey)
            decoded = blte(encoded); assert len(decoded) == size
            if output.exists(): assert output.read_bytes() == decoded
            output.parent.mkdir(parents=True, exist_ok=True); output.write_bytes(decoded)
        assert output.stat().st_size == size
        PROVENANCE[str(output.relative_to(ROOT))] = dict(url=url, range=f'{offset}-{offset+n-1}', ekey=fullkey, bytes=size, sha256=hashlib.sha256(output.read_bytes()).hexdigest(), remote_roundtrip_verified=verify)
        return output.read_bytes()
    raise ValueError('Ekey absent from cached CDN indices: '+key)

def main():
    files = tvfs((CACHE / '.tools_war3mod.tvfs').read_bytes())
    for path, (key, size) in files.items():
        target = DATA / Path(path).name.lower()
        if path.startswith('units/') and target.is_file(): fetch(key, size, target)
    for variant in ['custom_v0', 'custom_v1', 'melee_v0']:
        key, size = files['_balance/'+variant+'.w3mod']
        b = fetch(key, size, CACHE / (variant+'.tvfs'))
        entries = tvfs(b)
        (CACHE / (variant+'-files.json')).write_text(json.dumps(entries, indent=2))
        selected = {p:v for p,v in entries.items() if p.lower().startswith('units/') and (p.lower().endswith('.slk') or p.lower().endswith('func.txt') or p.lower().endswith('miscdata.txt'))}
        for path, (key, size) in selected.items():
            fetch(key, size, DATA / variant / Path(path).name.lower())
        print(variant, len(entries), 'entries;', len(selected), 'tables extracted', flush=True)
    (CACHE / 'latest-provenance.json').write_text(json.dumps(PROVENANCE, indent=2)+'\n')
    compare()

def compare():
    from audit_combat_parity import slk
    from resolve_source_combat import source_field
    old = json.loads((ROOT/'data/derived/reforged-source-combat.json').read_text())
    sources = {}
    for variant in ['root', 'custom_v0', 'custom_v1', 'melee_v0']:
        def select(name):
            candidate = DATA/variant/name.lower()
            return candidate if candidate.exists() else DATA/name.lower()
        tables = {t:slk(select(t+'.slk'))[0] for t in ['UnitWeapons','UnitBalance','UnitData','UnitUI']}
        profiles = {}
        names = {p.name for p in DATA.glob('*unitfunc.txt')}
        if variant != 'root': names |= {p.name for p in (DATA/variant).glob('*unitfunc.txt')}
        for name in sorted(names):
            path = select(name); section = None
            for line_no, raw in enumerate(path.read_text(encoding='latin1').splitlines(), 1):
                line = raw.strip()
                if line.startswith('[') and line.endswith(']'): section = line[1:-1]
                elif section and '=' in line and not line.startswith('//'):
                    k,v = line.split('=',1)
                    profiles.setdefault(section,{})[k.lower()] = {'raw_value':v, 'source':str(path.relative_to(ROOT))+':'+str(line_no)}
        sources[variant] = (tables, {'metadata':slk(select('unitmetadata.slk'))[0], 'profiles':profiles})
    result = {'build':'2.0.4.23745', 'checked_utc':'2026-09-08', 'build_config':'9a94ff7d25781db6c09190cd69d52458', 'cdn_config':'bb855f9558e73ed8da351212f96f4ed9', 'map_info':old['map_info'], 'method':'Map explicit overrides win. Four source variants evaluated independently; overlay file replaces root file of same name. No engine dataset selector assumed. Identical non-null values across all variants are overlay-invariant; this is not a replay certification.', 'provenance':PROVENANCE, 'objects':[]}
    counts = {}
    for unit in old['objects']:
        obj = {k:unit[k] for k in ['id','base','name']}; obj['fields'] = {}
        for fid, historical in unit['fields'].items():
            current = {variant:source_field(unit['base'],fid,*source) for variant,source in sources.items()}
            vals = {k:v['value'] for k,v in current.items()}
            if historical['status'] == 'map_explicit':
                field = {'status':'map_explicit_verified','value':historical['value'],'source':historical['source']}
            elif all(v is not None for v in vals.values()) and len({json.dumps(v,sort_keys=True) for v in vals.values()}) == 1:
                field = {'status':'latest_overlay_invariant','value':next(iter(vals.values()))}
            elif any(v is not None for v in vals.values()):
                field = {'status':'unresolved_overlay_selection','values':vals}
            else:
                field = {'status':'unresolved_null_or_missing'}
            field['historical_candidate'] = historical['value']
            field['latest_sources'] = {k:{'value':v['value'],'raw':v.get('raw_value'),'source':v.get('source')} for k,v in current.items()}
            if field['status'] == 'latest_overlay_invariant' and field['value'] != historical['value']:
                field['historical_conflict'] = True
            obj['fields'][fid] = field
            counts[field['status']] = counts.get(field['status'],0)+1
        result['objects'].append(obj)
    result['counts'] = counts
    out = ROOT/'data/derived/reforged-latest-inheritance.json'
    out.write_text(json.dumps(result,ensure_ascii=False,separators=(',',':'))+'\n', encoding='utf-8')
    print(json.dumps(counts),flush=True)

if __name__ == '__main__': main()
