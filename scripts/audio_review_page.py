"""Builds Captures/audio-review/index.html to audition sound candidates.
Candidates are files named <clip>__<tag>.wav in Captures/audio-review; the installed clip is
RiskAI/Assets/RiskAI/Resources/Audio/<clip>.wav. Only clips listed in the given manifest(s) are shown.
Usage: python scripts/audio_review_page.py manifest.json [...]"""
import html, json, sys, pathlib
REPO = pathlib.Path(__file__).resolve().parent.parent
ROOT = REPO / "Captures" / "audio-review"
targets = {}
for m in sys.argv[1:]:
    for it in json.load(open(m, encoding="utf-8")):
        targets.setdefault(it["target"][:-4], []).append(it)
rows = []
last_group = None
for base, cands in targets.items():
    group = cands[0].get("group")
    if group and group != last_group:
        rows.append(f'<tr><td colspan="2" class="g">{html.escape(group)}</td></tr>')
        last_group = group
    installed = (REPO / "RiskAI/Assets/RiskAI/Resources/Audio" / f"{base}.wav").exists()
    cells = [f'<label><input type="radio" name="{base}" value="old" checked> actual<br><audio controls preload="none" src="../../RiskAI/Assets/RiskAI/Resources/Audio/{base}.wav"></audio></label>' if installed else f'<label><input type="radio" name="{base}" value="old" checked> no</label>']
    for c in cands:
        if not c.get("file"):
            continue
        tag = c["file"][:-4].split("__")[1]
        cells.append(f'<label title="{html.escape(c["prompt"])}"><input type="radio" name="{base}" value="{tag}"> {tag}<br><audio controls preload="none" src="{c["file"]}"></audio></label>')
    cells.append(f'<label><input type="radio" name="{base}" value="redo"> regenerar</label>')
    rows.append(f'<tr><th>{base}</th><td class="c">{"".join(cells)}<br><input class="n" data-k="{base}" placeholder="Notas (qué falta, qué sobra, referencia…)"></td></tr>')
page = f'''<!doctype html><meta charset="utf-8"><title>Revisión de sonidos</title>
<style>body{{font:14px system-ui;background:#1b1712;color:#eee;margin:16px}}table{{border-collapse:collapse;width:100%}}td,th{{border-bottom:1px solid #444;padding:8px;vertical-align:top;text-align:left}}.c label{{display:inline-block;margin:0 12px 6px 0}}audio{{height:28px;width:190px}}input.n,textarea{{width:100%;box-sizing:border-box;background:#2a241c;color:#eee;border:1px solid #555;padding:6px;margin-top:4px}}button{{font-size:16px;padding:8px 16px;margin:12px 0}}textarea{{height:110px}}</style>
<h1>Riesgus · revisión de sonidos</h1><p>Elige una opción por efecto y, si quieres, añade notas. Solo se copian los cambios (lo que dejes en «actual» no se envía). Pasa el ratón sobre un candidato para ver su prompt. Luego pulsa el botón y pega el texto en el chat.</p>
<table>{"".join(rows)}</table>
<p>Notas generales:</p><textarea id="g" placeholder="Impresión general, estilo, volumen…"></textarea>
<button onclick="out()">Copiar selección y notas</button><textarea id="o"></textarea>
<script>function out(){{const r={{pick:{{}},notes:{{}},general:document.getElementById('g').value}};document.querySelectorAll('input[type=radio]:checked').forEach(i=>{{if(i.value!=='old')r.pick[i.name]=i.value}});document.querySelectorAll('input.n').forEach(i=>{{if(i.value.trim())r.notes[i.dataset.k]=i.value.trim()}});const t=JSON.stringify(r);document.getElementById('o').value=t;navigator.clipboard&&navigator.clipboard.writeText(t)}}</script>'''
(ROOT / "index.html").write_text(page, encoding="utf-8")
print(len(targets), "clips")
