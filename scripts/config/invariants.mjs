// Cross-field invariants of units.json. validate.mjs runs them; build.mjs emits the same
// rules as Core/UnitConfigValidation.g.cs. Do not copy the checks into C# by hand.
const enums = {
  domain: 'UnitDomain',
  rangeMeasure: 'RangeMeasure',
  targeting: 'WeaponTargeting',
  delivery: 'WeaponDelivery',
  portraitSource: 'PortraitSource',
};

export const fileRules = [
  { error: 'units.json version {version} is not supported', when: ['neq', ['get', 'version'], ['lit', 1]] },
  { error: 'units.json has no units', when: ['or', ['not', ['has', 'units']], ['eq', ['len', 'units'], ['lit', 0]]] },
];

export const unitRules = [
  { error: '{at}: heal/roar require mana', when: ['and', ['or', ['has', 'capabilities.heal'], ['has', 'capabilities.roar']], ['not', ['has', 'capabilities.mana']]] },
  { error: '{at}: transport is only for sea units', when: ['and', ['has', 'capabilities.transport'], ['neq', ['get', 'domain'], ['lit', 'Sea']]] },
  { error: '{at}: land units need collision and body radii', when: ['and', ['eq', ['get', 'domain'], ['lit', 'Land']], ['or', ['not', ['has', 'collision']], ['not', ['has', 'body']]]] },
  { error: '{at}: sea units need a hull', when: ['and', ['eq', ['get', 'domain'], ['lit', 'Sea']], ['not', ['has', 'hull']]] },
  { error: '{at}: hostWeapons are only for static posts', when: ['and', ['has', 'hostWeapons'], ['neq', ['get', 'domain'], ['lit', 'Static']]] },
  { error: '{at}: acquisition without a weapon', when: ['and', ['has', 'acquisition'], ['and', ['eq', ['len', 'weapons'], ['lit', 0]], ['not', ['has', 'hostWeapons']]]] },
  { error: '{at}: a weapon needs acquisition', when: ['and', ['not', ['has', 'acquisition']], ['or', ['gt', ['len', 'weapons'], ['lit', 0]], ['has', 'hostWeapons']]] },
  { error: '{at}: a unit carries at most one weapon', when: ['gt', ['len', 'weapons'], ['lit', 1]] },
  { error: '{at}: a variant portrait is a land unit', when: ['and', ['eq', ['get', 'presentation.portraitSource'], ['lit', 'Variant']], ['neq', ['get', 'domain'], ['lit', 'Land']]] },
  { error: '{at}: a land model portrait needs a model name', when: ['and', ['eq', ['get', 'domain'], ['lit', 'Land']], ['and', ['eq', ['get', 'presentation.portraitSource'], ['lit', 'Model']], ['not', ['has', 'presentation.model']]]] },
  { error: '{at}: a sea portrait uses a hull camera', when: ['and', ['eq', ['get', 'domain'], ['lit', 'Sea']], ['or', ['not', ['has', 'presentation.portraitCamera']], ['not', ['eq', ['get', 'presentation.portraitCamera.ship'], ['lit', true]]]]] },
  { error: '{at}: a hull camera is only for a sea unit', when: ['and', ['neq', ['get', 'domain'], ['lit', 'Sea']], ['and', ['has', 'presentation.portraitCamera'], ['eq', ['get', 'presentation.portraitCamera.ship'], ['lit', true]]]] },
];

export const weaponRules = [
  { error: '{w}: BodyEdges needs body.radius', when: ['and', ['eq', ['get', 'rangeMeasure'], ['lit', 'BodyEdges']], ['not', ['has', 'unit.body']]] },
  { error: '{w}: BodyEdges needs reach margins', when: ['and', ['eq', ['get', 'rangeMeasure'], ['lit', 'BodyEdges']], ['not', ['has', 'reach']]] },
  { error: '{w}: LaunchPoint needs Artillery delivery', when: ['and', ['eq', ['get', 'targeting'], ['lit', 'LaunchPoint']], ['neq', ['get', 'delivery'], ['lit', 'Artillery']]] },
  { error: '{w}: projectileSpeed must be 0 exactly for Instant delivery', when: ['neq', ['eq', ['get', 'delivery'], ['lit', 'Instant']], ['eq', ['get', 'projectileSpeed'], ['lit', 0]]] },
  { error: '{w}: minRange above range', when: ['gt', ['get', 'minRange'], ['get', 'range']] },
  { error: '{w}: flightTime max below min', when: ['and', ['has', 'flightTime'], ['and', ['has', 'flightTime.max'], ['lt', ['get', 'flightTime.max'], ['get', 'flightTime.min']]]] },
  { error: '{w}: splash needs three rings', when: ['and', ['has', 'splash'], ['neq', ['len', 'splash.rings'], ['lit', 3]]], mark: 'skipSplash' },
  { error: '{w}: the first splash ring deals full damage', when: ['and', ['has', 'splash'], ['neq', ['get', 'splash.rings.0.factor'], ['lit', 1]]], gate: 'skipSplash' },
  { error: '{w}: splash rings must not shrink', when: ['and', ['has', 'splash'], ['shrink', 'splash.rings']], gate: 'skipSplash' },
  { error: '{w}: splash needs a positive outer radius', when: ['and', ['has', 'splash'], ['lte', ['get', 'splash.rings.last.radius'], ['lit', 0]]], gate: 'skipSplash' },
];

export const duplicateMessage = ': duplicate id';
export const sharedClearanceMessage = 'every sea hull shares one positive clearance (the sea grid is global)';

function child(root, path) {
  const parts = path.split('.');
  let cursor = root;
  for (const part of parts) {
    if (cursor == null) return undefined;
    if (part === 'last') {
      if (!Array.isArray(cursor) || cursor.length === 0) return undefined;
      cursor = cursor[cursor.length - 1];
    } else if (/^\d+$/.test(part)) cursor = cursor[Number(part)];
    else cursor = cursor[part];
  }
  return cursor;
}

function evalExpr(expr, ctx) {
  const op = expr[0];
  if (op === 'lit') return expr[1];
  if (op === 'get') return child(ctx.root, expr[1]);
  if (op === 'has') return child(ctx.root, expr[1]) != null;
  if (op === 'len') {
    const value = child(ctx.root, expr[1]);
    return value == null ? 0 : value.length;
  }
  if (op === 'not') return !evalExpr(expr[1], ctx);
  if (op === 'and') return evalExpr(expr[1], ctx) && evalExpr(expr[2], ctx);
  if (op === 'or') return evalExpr(expr[1], ctx) || evalExpr(expr[2], ctx);
  if (op === 'eq') return evalExpr(expr[1], ctx) === evalExpr(expr[2], ctx);
  if (op === 'neq') return evalExpr(expr[1], ctx) !== evalExpr(expr[2], ctx);
  if (op === 'gt') return evalExpr(expr[1], ctx) > evalExpr(expr[2], ctx);
  if (op === 'lt') return evalExpr(expr[1], ctx) < evalExpr(expr[2], ctx);
  if (op === 'lte') return evalExpr(expr[1], ctx) <= evalExpr(expr[2], ctx);
  if (op === 'shrink') {
    const rings = child(ctx.root, expr[1]);
    if (!Array.isArray(rings)) return false;
    for (let i = 1; i < rings.length; i++) if (rings[i].radius < rings[i - 1].radius) return true;
    return false;
  }
  throw new Error('Unknown invariant op ' + op);
}

function fill(template, ctx) {
  return template.replaceAll('{version}', String(ctx.file?.version)).replaceAll('{at}', ctx.at ?? '').replaceAll('{w}', ctx.w ?? '');
}

export function invariants(file) {
  const errors = [];
  if (!file) { errors.push('units.json is empty'); return errors; }
  const fileCtx = { root: file, file };
  for (const rule of fileRules) if (evalExpr(rule.when, fileCtx)) errors.push(fill(rule.error, fileCtx));
  if (!Array.isArray(file.units)) return errors;
  const seen = new Set();
  let clearance = null;
  for (const unit of file.units) {
    if (!unit) { errors.push('null unit entry'); continue; }
    const at = `units[${unit.id}]`;
    if (seen.has(unit.id)) errors.push(`${at}${duplicateMessage}`);
    seen.add(unit.id);
    const unitCtx = { root: unit, file, at };
    for (const rule of unitRules) if (evalExpr(rule.when, unitCtx)) errors.push(fill(rule.error, unitCtx));
    if (unit.domain === 'Sea' && unit.hull) {
      if (!(unit.hull.clearance > 0) || (clearance != null && unit.hull.clearance !== clearance)) errors.push(sharedClearanceMessage);
      clearance = unit.hull.clearance;
    }
    const weapons = [...(unit.weapons ?? []), ...(unit.hostWeapons ? [unit.hostWeapons.town, unit.hostWeapons.harbor] : [])];
    for (const weapon of weapons) {
      if (!weapon) continue;
      const w = `${at}.weapon[${weapon.source}]`;
      const weaponCtx = { root: { ...weapon, unit }, file, at, w };
      let skipSplash = false;
      for (const rule of weaponRules) {
        if (skipSplash && rule.error.includes('splash') && !rule.error.includes('three rings')) continue;
        if (evalExpr(rule.when, weaponCtx)) {
          errors.push(fill(rule.error, weaponCtx));
          if (rule.error.includes('three rings')) skipSplash = true;
        }
      }
    }
  }
  return errors;
}

function pascal(name) {
  return name.replace(/(^|\.)([a-z])/g, (_, dot, letter) => dot + letter.toUpperCase());
}

function chain(path, receiver) {
  const parts = path.split('.');
  let code = receiver;
  const guards = [];
  for (let i = 0; i < parts.length; i++) {
    const part = parts[i];
    if (part === 'last') { code = `${code}[${code}.Length - 1]`; continue; }
    if (/^\d+$/.test(part)) { code = `${code}[${part}]`; continue; }
    const next = `${code}.${pascal(part)}`;
    if (i < parts.length - 1) guards.push(`${next} != null`);
    code = next;
  }
  return { code, guards };
}

function receiverOf(path, hint) {
  if (path.startsWith('unit.')) return { recv: 'unit', path: path.slice(5) };
  if (hint === 'weapon') return { recv: 'weapon', path };
  if (hint === 'file') return { recv: 'file', path };
  return { recv: 'unit', path };
}

function csExpr(expr, hint) {
  const op = expr[0];
  if (op === 'lit') return typeof expr[1] === 'string' ? JSON.stringify(expr[1]) : String(expr[1]);
  if (op === 'get' || op === 'has' || op === 'len') {
    const raw = expr[1];
    const shifted = receiverOf(raw, hint);
    const access = chain(shifted.path, shifted.recv);
    if (op === 'has') {
      const present = `${access.code} != null`;
      return access.guards.length ? `(${access.guards.join(' && ')} && ${present})` : present;
    }
    if (op === 'len') {
      const length = `(${access.code} == null ? 0 : ${access.code}.Length)`;
      return access.guards.length ? `(${access.guards.join(' && ')} ? ${length} : 0)` : length;
    }
    return access.code;
  }
  if (op === 'not') return `!(${csExpr(expr[1], hint)})`;
  if (op === 'and') return `(${csExpr(expr[1], hint)} && ${csExpr(expr[2], hint)})`;
  if (op === 'or') return `(${csExpr(expr[1], hint)} || ${csExpr(expr[2], hint)})`;
  const map = { eq: '==', neq: '!=', gt: '>', lt: '<', lte: '<=' };
  if (map[op]) {
    const right = expr[2];
    if (op !== 'gt' && op !== 'lt' && op !== 'lte' && right[0] === 'lit' && typeof right[1] === 'string' && expr[1][0] === 'get') {
      const leaf = expr[1][1].split('.').pop();
      const type = enums[leaf];
      if (type) {
        const shifted = receiverOf(expr[1][1], hint);
        const access = chain(shifted.path, shifted.recv);
        const compare = `${access.code} ${map[op]} ${type}.${right[1]}`;
        return access.guards.length ? `(${access.guards.join(' && ')} && ${compare})` : compare;
      }
    }
    if ((op === 'eq' || op === 'neq') && expr[1][0] === 'eq') {
      // (delivery == Instant) != (projectileSpeed == 0)
      return `(${csExpr(expr[1], hint)}) ${map[op]} (${csExpr(right, hint)})`;
    }
    return `${csExpr(expr[1], hint)} ${map[op]} ${csExpr(right, hint)}`;
  }
  if (op === 'shrink') return 'SplashShrinks(weapon.Splash.Rings)';
  throw new Error('Cannot emit ' + op);
}

function csError(template) {
  return template
    .replaceAll('{version}', '" + file.Version + "')
    .replaceAll('{at}', '" + at + "')
    .replaceAll('{w}', '" + w + "');
}

function emitRule(rule, hint) {
  const message = csError(rule.error);
  return `if (${csExpr(rule.when, hint)}) errors.Add("${message}");`;
}

export function csharpValidation() {
  const fileChecks = fileRules.map((rule) => '            ' + emitRule(rule, 'file')).join('\n');
  const unitChecks = unitRules.map((rule) => '                ' + emitRule(rule, 'unit')).join('\n');
  const weaponChecks = weaponRules.map((rule) => {
    const message = csError(rule.error);
    const test = csExpr(rule.when, 'weapon');
    if (rule.mark === 'skipSplash') return `                if (${test}) { errors.Add("${message}"); skipSplash = true; }`;
    if (rule.gate === 'skipSplash') return `                if (!skipSplash && ${test}) errors.Add("${message}");`;
    return `                if (${test}) errors.Add("${message}");`;
  }).join('\n');
  return `// <auto-generated>
// Generated by scripts/config/invariants.mjs. Do not edit.
// Regenerate: cd scripts/config && npm run build
// </auto-generated>
using System.Collections.Generic;

namespace RiskAI.Core
{
    /// <summary>Cross-field invariants of units.json, generated from scripts/config/invariants.mjs.</summary>
    public static class UnitConfigValidation
    {
        public static List<string> Errors(UnitsFile file)
        {
            var errors = new List<string>();
            if (file == null) { errors.Add("units.json is empty"); return errors; }
${fileChecks}
            if (file.Units == null || file.Units.Length == 0) return errors;
            var seen = new HashSet<string>();
            float? clearance = null;
            foreach (var unit in file.Units)
            {
                if (unit == null) { errors.Add("null unit entry"); continue; }
                string at = "units[" + unit.Id + "]";
                if (!seen.Add(unit.Id)) errors.Add(at + "${duplicateMessage}");
${unitChecks}
                if (unit.Domain == UnitDomain.Sea && unit.Hull != null)
                {
                    if (!(unit.Hull.Clearance > 0) || clearance != null && unit.Hull.Clearance != clearance) errors.Add("${sharedClearanceMessage}");
                    clearance = unit.Hull.Clearance;
                }
                if (unit.Weapons != null) foreach (var weapon in unit.Weapons) Check(unit, weapon, at, errors);
                if (unit.HostWeapons != null) { Check(unit, unit.HostWeapons.Town, at, errors); Check(unit, unit.HostWeapons.Harbor, at, errors); }
            }
            return errors;
        }

        static void Check(UnitConfig unit, UnitWeapon weapon, string at, List<string> errors)
        {
            if (weapon == null) return;
            string w = at + ".weapon[" + weapon.Source + "]";
            bool skipSplash = false;
${weaponChecks}
        }

        static bool SplashShrinks(SplashRing[] rings)
        {
            if (rings == null) return false;
            for (int i = 1; i < rings.Length; i++) if (rings[i].Radius < rings[i - 1].Radius) return true;
            return false;
        }
    }
}
`;
}
