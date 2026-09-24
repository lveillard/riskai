// Validates RiskAI/Assets/RiskAI/Resources/Config/units.json: strict schema plus cross-field invariants.
// The runtime loader (UnitConfigLoader.cs) enforces the same invariants.
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import Ajv2020 from 'ajv/dist/2020.js';
import { schemaDocument, unitsPath } from './build.mjs';
import { invariants } from './invariants.mjs';
export { unitsPath, invariants };

const here = dirname(fileURLToPath(import.meta.url));

const ajv = new Ajv2020({ allErrors: true, strict: true });
const validateSchema = ajv.compile(schemaDocument());

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
