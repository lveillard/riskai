"""Write docs/RISK-RULES-v0.34.md from units.json.

Source fields come from the unit's `source` object. Every string in
`adaptation` is a local deviation and is marked as such. Re-run after
editing Resources/Config/units.json:

    python scripts/generate_unit_rules.py
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
UNITS = ROOT / "RiskAI" / "Assets" / "RiskAI" / "Resources" / "Config" / "units.json"
OUT = ROOT / "docs" / "RISK-RULES-v0.34.md"


def damage(weapon):
    if not weapon:
        return "—"
    base, dice, sides = weapon["base"], weapon["dice"], weapon["sides"]
    if dice:
        return f"{base + dice}–{base + dice * sides} ({base}+{dice}d{sides})"
    return str(base)


def weapon_line(weapon):
    if not weapon:
        return "sin arma"
    reach = weapon.get("range")
    measure = weapon.get("rangeMeasure", "")
    delivery = weapon.get("delivery", "")
    minimum = weapon.get("minRange") or 0
    extra = f", mínimo {minimum}" if minimum else ""
    return f"{damage(weapon)} cada {weapon['cooldown']} s, alcance {reach} ({measure}, {delivery}{extra})"


def main():
    data = json.loads(UNITS.read_text(encoding="utf-8"))
    lines = [
        "# Reglas de unidades v0.34",
        "",
        "Generado por `scripts/generate_unit_rules.py` a partir de "
        "`RiskAI/Assets/RiskAI/Resources/Config/units.json`. "
        "Ese JSON es la única fuente de los números. "
        "Lo que viene de Warcraft 3 está en `source`. "
        "Cada entrada de `adaptation` es una desviación local y está marcada abajo como **adaptación**.",
        "",
        f"Versión del catálogo: {data['version']}. Tipos: {len(data['units'])}.",
        "",
        "La casilla de producción no está en el JSON: `ProductionHotkeys` la calcula "
        "(coste, orden estable, tierra antes que mar).",
        "",
    ]
    for unit in data["units"]:
        names = unit["names"]
        source = unit.get("source") or {}
        lines.append(f"## {names['es']} ({unit['id']})")
        lines.append("")
        lines.append(f"- Nombre en inglés: {names['en']}.")
        role = unit.get("role") or {}
        if role.get("es"):
            lines.append(f"- Rol: {role['es']} / {role.get('en', '')}.")
        raw = source.get("rawcode") or "sin rawcode"
        base = source.get("base") or "sin base"
        lines.append(f"- **Fuente:** {raw} · {base}.")
        if source.get("notes"):
            lines.append(f"- Nota de fuente: {source['notes']}.")
        for item in unit.get("adaptation") or []:
            lines.append(f"- **Adaptación:** {item}")
        movement = unit["movement"]
        lines.append(
            f"- Dominio {unit['domain']}, edificio {unit['building']}, "
            f"coste {unit['cost']}, puntos {unit['points']}, "
            f"entrenamiento {unit['trainSeconds']} s, nivel {unit['level']}."
        )
        lines.append(
            f"- Vida {unit['maxHealth']}, armadura {unit['armor']} {unit['armorType']}, "
            f"velocidad {movement['speed']}"
            f"{', penalización de bosque' if movement.get('forestPenalty') else ''}."
        )
        if not unit.get("canBeAttacked", True):
            lines.append("- No se puede atacar: se ataca a su guardián o se captura el edificio.")
        weapons = unit.get("weapons") or []
        if weapons:
            lines.append(f"- Arma: {weapon_line(weapons[0])}.")
        host = unit.get("hostWeapons")
        if host:
            lines.append(f"- Arma de ciudad: {weapon_line(host['town'])}.")
            lines.append(f"- Arma de puerto: {weapon_line(host['harbor'])}.")
        acquisition = unit.get("acquisition")
        if acquisition:
            radius = acquisition["radius"]
            lines.append(
                f"- Adquisición: hostil {radius['hostile']}, neutral {radius['neutral']}, "
                f"mantener posición {radius['hold']} ({acquisition.get('measure', '')}, "
                f"{acquisition.get('visibility', '')})."
            )
        caps = unit.get("capabilities") or {}
        flags = []
        if caps.get("canCapture"):
            flags.append("captura")
        if caps.get("canGarrison"):
            flags.append("guarnición")
        if caps.get("canEmbark"):
            flags.append("embarca")
        if caps.get("harborGuard"):
            flags.append("guardia de puerto")
        transport = caps.get("transport")
        if transport:
            flags.append(f"transporte {transport['capacity']} plazas, radio {transport['loadRadius']}")
        if caps.get("heal"):
            heal = caps["heal"]
            flags.append(f"cura {heal['amount']} a {heal['range']}")
        if caps.get("roar"):
            roar = caps["roar"]
            flags.append(f"rugido {roar['area']} durante {roar['duration']} s")
        if flags:
            lines.append("- Capacidades: " + ", ".join(flags) + ".")
        lines.append("")
    OUT.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
