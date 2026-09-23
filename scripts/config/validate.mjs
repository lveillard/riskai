// Validates RiskAI/Assets/RiskAI/Resources/Config/units.json: strict schema plus cross-field invariants.
// The runtime loader (UnitConfigLoader.cs) enforces the same invariants.
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import Ajv2020 from 'ajv/dist/2020.js';
import { schemaDocument, unitsPath } from './build.mjs';
export { unitsPath };

const here = dirname(fileURLToPath(import.meta.url));

const ajv = new Ajv2020({ allErrors: true, strict: true });
const validateSchema = ajv.compile(schemaDocument());

export function invariants(file) {
  const errors = [];
  const seen = new Set();
  for (const unit of file.units) {
    const at = `units[${unit.id}]`;
    if (seen.has(unit.id)) errors.push(`${at}: duplicate id`);
    seen.add(unit.id);
    const caps = unit.capabilities;
    if ((caps.heal || caps.roar) && !caps.mana) errors.push(`${at}: heal/roar require mana`);
    if (caps.transport && unit.domain !== 'Sea') errors.push(`${at}: transport is only for sea units`);
    if (unit.domain === 'Land' && (!unit.collision || !unit.body)) errors.push(`${at}: land units need collision and body radii`);
    if (unit.domain === 'Sea' && !unit.hull) errors.push(`${at}: sea units need a hull`);
    if (unit.hostWeapons && unit.domain !== 'Static') errors.push(`${at}: hostWeapons are only for static posts`);
    if (unit.acquisition && unit.weapons.length === 0 && !unit.hostWeapons) errors.push(`${at}: acquisition without a weapon`);
    if (!unit.acquisition && (unit.weapons.length > 0 || unit.hostWeapons)) errors.push(`${at}: a weapon needs acquisition`);
    const weapons = [...unit.weapons, ...(unit.hostWeapons ? [unit.hostWeapons.town, unit.hostWeapons.harbor] : [])];
    for (const weapon of weapons) {
      const w = `${at}.weapon[${weapon.source}]`;
      if (weapon.rangeMeasure === 'BodyEdges' && !unit.body) errors.push(`${w}: BodyEdges needs body.radius`);
      if (weapon.rangeMeasure === 'BodyEdges' && !weapon.reach) errors.push(`${w}: BodyEdges needs reach margins`);
      if (weapon.targeting === 'LaunchPoint' && weapon.delivery !== 'Artillery') errors.push(`${w}: LaunchPoint needs Artillery delivery`);
      if ((weapon.delivery === 'Instant') !== (weapon.projectileSpeed === 0)) errors.push(`${w}: projectileSpeed must be 0 exactly for Instant delivery`);
      if (weapon.minRange > weapon.range) errors.push(`${w}: minRange above range`);
      if (weapon.flightTime && weapon.flightTime.max !== undefined && weapon.flightTime.max < weapon.flightTime.min) errors.push(`${w}: flightTime max below min`);
      if (weapon.splash) {
        const r = weapon.splash.rings;
        if (r[0].factor !== 1) errors.push(`${w}: the first splash ring deals full damage`);
        for (let i = 1; i < r.length; i++) if (r[i].radius < r[i - 1].radius) errors.push(`${w}: splash rings must not shrink`);
        if (r[r.length - 1].radius <= 0) errors.push(`${w}: splash needs a positive outer radius`);
      }
    }
  }
  return errors;
}

export function validate(file) {
  if (!validateSchema(file)) return validateSchema.errors.map((e) => `${e.instancePath || '/'} ${e.message} ${JSON.stringify(e.params)}`);
  return invariants(file);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const file = JSON.parse(readFileSync(unitsPath, 'utf8'));
  const errors = validate(file);
  if (errors.length) { for (const error of errors) console.error(error); process.exit(1); }
  console.log(`units.json valid: ${file.units.length} units`);
}
