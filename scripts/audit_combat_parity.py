"""Emit all map objects with field-level W3U / historical RoC provenance.

No absent field is assigned a Warcraft default. RoC candidates are explicitly
not certified as the Reforged base patch. Run with Python, no game/editor needed.
"""
import json
import re
from pathlib import Path
from audit_reforged_units import ROOT, DEFAULT_MAP, Reader, parse_w3u, parse_wts, resolve_text

FIELDS = {
    'uhpm':'HP', 'udef':'def', 'udty':'defType', 'umvs':'spd',
    'ugol':'goldcost', 'ulum':'lumbercost', 'uupg':'upgrades',
    'uacq':'acquire', 'uamn':'minRange', 'uaen':'weapsOn',
}
for weapon in (1, 2):
    for field, column in {
        'b':'dmgplus', 'd':'dice', 's':'sides', 'c':'cool',
        'r':'rangeN', 't':'atkType', 'w':'weapTp', 'g':'targs',
        'f':'Farea', 'h':'Harea', 'q':'Qarea', 'p':'splashTargs', 'u':'dmgUp',
    }.items():
        FIELDS[f'ua{weapon}{field}'] = f'{column}{weapon}'
    FIELDS[f'ua{weapon}z'] = None  # missile speed is not in these weapon SLKs
    for column in ('dmgpt', 'backSw', 'Hfact', 'Qfact'):
        FIELDS[f'slk.{column}{weapon}'] = f'{column}{weapon}'


def slk(path):
    data = path.read_bytes()
    if not data.startswith(b'ID;'):
        return {}, 'not_text_slk'
    cells = {}
    x = y = 1
    for line_number, line in enumerate(data.decode('latin1').splitlines(), 1):
        if not line.startswith('C;'):
            continue
        for token in re.findall(r'(?:[^;\"]|\"(?:[^\"]|\"\")*\")+', line):
            if token.startswith('X'): x = int(token[1:])
            elif token.startswith('Y'): y = int(token[1:])
            elif token.startswith('K'):
                raw = token[1:]
                try: value = json.loads(raw)
                except (ValueError, TypeError): value = raw.strip('"')
                cells[x, y] = (value, line_number)
    headers = {x: value for (x, y), (value, _) in cells.items() if y == 1}
    rows = {}
    for y in sorted({y for x, y in cells if y > 1}):
        if (1, y) not in cells: continue
        rows[cells[1, y][0]] = {
            headers[x]: {'value':value, 'source':f'{path.relative_to(ROOT).as_posix()}:{line}'}
            for (x, cy), (value, line) in cells.items() if cy == y and x in headers
        }
    return rows, 'historical_RoC_candidate'


def main():
    tables = {}
    inputs = {}
    for path in sorted((ROOT/'references/owned-disc-data').glob('*-Unit*.slk')):
        rows, status = slk(path)
        inputs[path.relative_to(ROOT).as_posix()] = status
        if path.name.startswith('RoC-'):
            for raw, fields in rows.items(): tables.setdefault(raw, {}).update(fields)
    _, base, custom, parsed = parse_w3u(DEFAULT_MAP/'war3map.w3u')
    strings, names = parse_wts(DEFAULT_MAP/'war3map.wts')
    upgrade_reader = Reader((DEFAULT_MAP/'war3map.w3q').read_bytes())
    upgrade_version = upgrade_reader.int32()
    upgrades = []
    for table in ('base', 'custom'):
        for _ in range(upgrade_reader.int32()):
            old, new = upgrade_reader.tag(), upgrade_reader.tag()
            if upgrade_version >= 3:
                unknown = [upgrade_reader.int32() for _ in range(upgrade_reader.int32())]
            modifications = []
            for _ in range(upgrade_reader.int32()):
                offset = upgrade_reader.pos
                field, kind = upgrade_reader.tag(), upgrade_reader.int32()
                level, pointer = upgrade_reader.int32(), upgrade_reader.int32()
                value = upgrade_reader.value(kind)
                check = upgrade_reader.tag()
                modifications.append({'field':field, 'level':level, 'pointer':pointer,
                    'raw_value':value, 'value':resolve_text(value, strings),
                    'source':f'references/maps/reforged-v3-source/war3map.w3q@0x{offset:x}'})
            upgrades.append({'id':new if new.strip('\0') else old, 'base':old,
                'table':table, 'modifications':modifications})
    assert upgrade_reader.pos == len(upgrade_reader.data), 'W3Q must be fully consumed'
    base_mods = {r.old_id:r.modifications for r in base}
    objects = []
    for record in base + custom:
        raw = record.new_id if record.new_id.strip('\0') else record.old_id
        fields = {}
        overrides = {**base_mods.get(record.old_id, {}), **record.modifications}
        for field, column in FIELDS.items():
            if field in overrides:
                mod = overrides[field]
                fields[field] = {'value':mod.value, 'status':'map_explicit',
                    'source':f'references/maps/reforged-v3-source/war3map.w3u@0x{mod.offset:x}', 'column':column}
            elif column in tables.get(record.old_id, {}):
                fields[field] = {**tables[record.old_id][column], 'status':'historical_RoC_candidate', 'column':column}
            else:
                fields[field] = {'value':None, 'status':'unresolved', 'column':column}
        objects.append({'id':raw, 'base':record.old_id, 'name':names.get(raw, raw),
            'fields':fields, 'all_map_overrides':{
                key:{'value':resolve_text(mod.value, strings), 'offset':hex(mod.offset)}
                for key, mod in record.modifications.items()}})
    jass = (DEFAULT_MAP/'war3map.j').read_text(encoding='utf-8')
    triggers = [{'line':n, 'text':line.strip()} for n,line in enumerate(jass.splitlines(),1)
                if re.search(r'SetPlayerTechResearched|DecPlayerTechResearched|BlzSetUnit(BaseDamage|Dice|MaxHP)|UnitDamageTarget',line)]
    result = {'scope':'All 69 W3U objects, unupgraded static fields; runtime trigger effects separate',
        'warning':'RoC is historical candidate inheritance, not verified Reforged patch. Null means unresolved, never zero.',
        'w3u_bytes_parsed':parsed, 'base_inputs':inputs, 'objects':objects, 'combat_trigger_refs':triggers,
        'upgrade_objects':upgrades, 'w3q_bytes_parsed':upgrade_reader.pos}
    out = ROOT/'data/derived/reforged-combat-parity.json'
    out.write_text(json.dumps(result, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')
    selected = ['uhpm','ua1b','ua1d','ua1s','ua1t','udty','udef','ua1r','ua1c','slk.dmgpt1','slk.backSw1','ua1w','ua1z','uupg']
    lines = ['# Complete static object combat audit', '',
        '> Superseded for implementation by `data/derived/reforged-source-combat.json` and `docs/audits/reforged-source-resolution.md`, which resolve fields through TFT metadata/profiles and include W3A, W3Q and the effective roster. This table is retained as the original RoC-only comparison.', '',
        'Generated by `python scripts/audit_combat_parity.py`. Every W3U object is included; inclusion does not imply availability in every mode. Values suffixed **R** are historical RoC inheritance candidates; **?** is unresolved. Unsuffixed values are explicit map overrides. JSON contains binary offsets, SLK lines, every override, both weapons, splash, and JASS technology mutations. These values alone are not a claim of complete computational parity.', '',
        '| ID ← base / name | '+' | '.join(selected)+' |',
        '|---|'+'---|'*len(selected)]
    for obj in objects:
        values=[]
        for key in selected:
            item=obj['fields'][key]
            value=item['value']
            if isinstance(value,float): value=f'{value:.6g}'
            values.append('?' if value is None else str(value).replace('|','/')+(' R' if item['status']=='historical_RoC_candidate' else ''))
        lines.append(f"| {obj['id']} ← {obj['base']} / {obj['name'].replace('|','/')} | "+' | '.join(values)+' |')
    (ROOT/'docs/audits/reforged-complete-combat-table.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print(f'{len(objects)} objects; {parsed} W3U bytes; {len(triggers)} trigger references; wrote {out}')


if __name__ == '__main__': main()
