import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { validate, unitsPath } from '../validate.mjs';
import { csharp, schemaJson, csharpPath, schemaPath, unitKindSource, unitKindPath, validationPath } from '../build.mjs';
import { csharpValidation } from '../invariants.mjs';

const load = () => JSON.parse(readFileSync(unitsPath, 'utf8'));
const find = (file, id) => file.units.find((unit) => unit.id === id);

test('shipped units.json is valid', () => assert.deepEqual(validate(load()), []));

test('generated outputs are committed', () => {
  assert.equal(readFileSync(schemaPath, 'utf8').replace(/\r\n/g, '\n'), schemaJson());
  assert.equal(readFileSync(csharpPath, 'utf8').replace(/\r\n/g, '\n'), csharp());
  assert.equal(readFileSync(validationPath, 'utf8').replace(/\r\n/g, '\n'), csharpValidation());
});

test('unknown fields are rejected', () => {
  const file = load();
  find(file, 'Footman').speedBonus = 2;
  assert.ok(validate(file).some((e) => e.includes('additional')));
});

test('enums are text and unknown members are rejected', () => {
  const file = load();
  find(file, 'Footman').armorType = 'Plate';
  assert.notDeepEqual(validate(file), []);
});

test('duplicate ids are rejected', () => {
  const file = load();
  file.units.push(structuredClone(find(file, 'Archer')));
  assert.ok(validate(file).some((e) => e.includes('duplicate id')));
});

test('heal requires mana', () => {
  const file = load();
  delete find(file, 'Medic').capabilities.mana;
  assert.ok(validate(file).some((e) => e.includes('require mana')));
});

test('transport is only for sea units', () => {
  const file = load();
  find(file, 'Footman').capabilities.transport = { capacity: 1, loadRadius: 1, loadLimit: 1 };
  assert.ok(validate(file).some((e) => e.includes('only for sea')));
});

test('BodyEdges requires a body radius', () => {
  const file = load();
  delete find(file, 'Knight').body;
  assert.ok(validate(file).some((e) => e.includes('BodyEdges needs body.radius')));
});

test('LaunchPoint requires Artillery delivery', () => {
  const file = load();
  find(file, 'Mortar').weapons[0].delivery = 'Missile';
  assert.ok(validate(file).some((e) => e.includes('LaunchPoint needs Artillery')));
});

test('a variant portrait is a land unit', () => {
  const file = load();
  find(file, 'Frigate').presentation.portraitSource = 'Variant';
  assert.ok(validate(file).some((e) => e.includes('variant portrait is a land unit')));
});

test('exactly one land camera is the shared default', () => {
  const file = load();
  const marked = file.units.filter((unit) => unit.presentation.portraitCamera.landDefault === true);
  assert.equal(marked.length, 1);
  assert.equal(marked[0].domain, 'Land');
  find(file, 'Footman').presentation.portraitCamera.landDefault = false;
  assert.ok(validate(file).some((e) => e.includes('exactly one landDefault')));
});

test('a unit without a presentation block is a readable error', () => {
  const file = load();
  delete find(file, 'Footman').presentation;
  const errors = validate(file);
  assert.ok(errors.length > 0);
  assert.ok(errors.some((error) => error.toLowerCase().includes('presentation')));
  assert.ok(errors.every((error) => !error.includes('NullReference')));
});

test('generated validation guards a missing portrait camera', () => {
  const source = csharpValidation();
  assert.match(source, /Presentation != null && unit\.Presentation\.PortraitCamera != null && \(unit\.Presentation\.PortraitCamera\.LandDefault == true\)/);
});

test('a land model portrait needs a model name', () => {
  const file = load();
  find(file, 'Mage').presentation.model = null;
  assert.ok(validate(file).some((e) => e.includes('needs a model name')));
});
