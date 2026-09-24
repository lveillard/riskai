// Riesgus unit contract (v0.34). Single source of the units.json shape: build.mjs emits
// units.schema.json and the C# DTO RiskAI/Assets/RiskAI/Scripts/Core/UnitConfig.g.cs from it.
// Every named object/enum carries a $id: that is its generated C# type name.
import { Type, type TSchema } from '@sinclair/typebox';

const strict = { additionalProperties: false } as const;
const Named = <T extends TSchema>(id: string, description: string, schema: T): T =>
  Object.assign(schema, { $id: id, description });
const Enum = (id: string, description: string, values: string[]) =>
  Named(id, description, Type.Union(values.map((value) => Type.Literal(value))));
const NonNegative = (description: string) => Type.Number({ minimum: 0, description });
const Count = (description: string) => Type.Integer({ minimum: 0, description });

// ---- enums: member order is the C# ordinal (CombatRules indexes AttackKind rows) ----
export const AttackKind = Enum('AttackKind', 'Warcraft attack type (damage table row).',
  ['Normal', 'Piercing', 'Siege', 'Magic', 'Chaos', 'Hero', 'Spells']);
export const ArmorKind = Enum('ArmorKind', 'Warcraft defense type.',
  ['Unarmored', 'Light', 'Medium', 'Heavy', 'Fortified', 'Normal', 'Hero', 'Divine']);
export const WeaponDelivery = Enum('WeaponDelivery', 'How a hit travels: instant, homing missile, artillery arc, missile with splash.',
  ['Instant', 'Missile', 'Artillery', 'MissileSplash']);
export const WeaponTargeting = Enum('WeaponTargeting', 'Target follows the victim; LaunchPoint fixes the impact where it stood at release.',
  ['Target', 'LaunchPoint']);
// Flag bit = position (Air = 1 << 0 ... Soldier = 1 << 12), same bits as the Warcraft target flags.
export const WeaponTargetFlag = Enum('WeaponTargetMask', 'Target classes and relations; "Soldier" is the local class used by the Mage.',
  ['Air', 'Debris', 'Ground', 'Item', 'Structure', 'Ward', 'Self', 'Tree', 'Wall', 'Enemy', 'Neutral', 'Ally', 'Soldier']);
export const UnitDomain = Enum('UnitDomain', 'Where the unit moves: NavMesh land, SeaNavigation water, or fixed.',
  ['Land', 'Sea', 'Static']);
export const UnitBuilding = Enum('UnitBuilding', 'Building that produces the unit. The grid cell is computed by ProductionHotkeys.',
  ['None', 'City', 'Harbor']);
export const RangeMeasure = Enum('RangeMeasure',
  'BodyEdges: 3D to the target approach point minus both body radii. CenterToApproach: 3D pivot to approach point. ' +
  'ToHull: XZ pivot to approach point (a ship hull). CenterToCenter: XZ pivot to pivot.',
  ['BodyEdges', 'CenterToApproach', 'ToHull', 'CenterToCenter']);
export const TieBreak = Enum('AcquisitionTieBreak', 'Equal scores: the lower entity id wins, or the first candidate found keeps it.',
  ['LowerEntityId', 'FirstFound']);
export const Visibility = Enum('UnitVisibility', 'Line of sight: terrain ray between aim points, or a NavMesh ray to the approach point.',
  ['TerrainRay', 'NavMeshRay']);

// ---- objects ----
export const Names = Named('UnitNames', 'Display text in both UI languages.', Type.Object({
  es: Type.String({ minLength: 1 }),
  en: Type.String({ minLength: 1 }),
}, strict));

export const Source = Named('UnitSource', 'Provenance: raw Warcraft id, source profile and notes.', Type.Object({
  rawcode: Type.Union([Type.String({ pattern: '^[a-zA-Z][a-zA-Z0-9]{3}$' }), Type.Null()]),
  base: Type.Optional(Type.String()),
  notes: Type.Optional(Type.String()),
}, strict));

export const Movement = Named('UnitMovement', 'Movement: base speed, forest drag on land, ship separation.', Type.Object({
  speed: NonNegative('Base speed in Unity metres per second (native / 50).'),
  forestPenalty: Type.Boolean({ description: 'Forest canopies slow this unit (land only).' }),
  separation: Type.Optional(NonNegative('Minimum distance kept from other units of the same domain (ships).')),
}, strict));

export const Radius = Named('UnitRadius', 'A circle radius in metres.', Type.Object({ radius: NonNegative('Radius in metres.') }, strict));

export const Hull = Named('UnitHull', 'Oriented ship hull: builds the NavalArt model scale and the target BoxCollider.', Type.Object({
  length: NonNegative('Collider length along the keel.'),
  beam: NonNegative('Collider width.'),
  height: NonNegative('Collider height.'),
  centerHeight: Type.Number({ description: 'Collider centre height above the pivot.' }),
  scale: NonNegative('Model scale relative to the base silhouette.'),
  warship: Type.Boolean({ description: 'Warship silhouette (false: transport silhouette).' }),
  clearance: NonNegative('Draft the sea grid keeps around this hull. Every hull shares one value.'),
}, strict));

export const Footprint = Named('UnitFootprint', 'Square building footprint (towers).', Type.Object({ size: NonNegative('Side length in metres.') }, strict));

export const StandingBounds = Named('UnitStandingBounds', 'Source MDX Stand bounds (native / 50).', Type.Object({
  height: NonNegative('Height.'),
  width: NonNegative('Width.'),
}, strict));

export const Visual = Named('UnitVisual', 'Picking and presentation sizes, independent from collision.', Type.Object({
  height: NonNegative('Picking/label height.'),
  radius: NonNegative('Picking radius (half the standing width).'),
  standingHeight: NonNegative('Rendered model height target; 0 keeps the model scale.'),
  standingWidth: NonNegative('Rendered model width target; 0 keeps the model scale.'),
  mdx: Type.Optional(StandingBounds),
}, strict));

export const Ring = Named('SplashRing', 'Area-damage ring: full damage inside the first, then each factor.', Type.Object({
  radius: NonNegative('Outer radius of this ring.'),
  factor: Type.Number({ minimum: 0, maximum: 1, description: 'Damage factor inside this ring.' }),
}, strict));

export const Splash = Named('WeaponSplash', 'Area damage by rings and the classes/relations it may hit.', Type.Object({
  rings: Type.Array(Ring, { minItems: 3, maxItems: 3 }),
  mask: Type.Array(WeaponTargetFlag),
}, strict));

export const FlightTime = Named('WeaponFlightTime', 'Projectile flight time clamp in seconds; no max means unbounded.', Type.Object({
  min: NonNegative('Minimum flight time.'),
  max: Type.Optional(NonNegative('Maximum flight time.')),
}, strict));

export const Reach = Named('WeaponReach', 'Melee hysteresis: first blow this far inside the reach; path this far inside it.', Type.Object({
  approachMargin: NonNegative('Path target inside the reach.'),
  holdMargin: NonNegative('First blow inside the reach.'),
}, strict));

export const WeaponSound = Enum('WeaponSound',
  'Which clip plays for this weapon. Presentation reads this field; it does not list unit kinds.',
  ['Blade', 'Lance', 'Bow', 'Firearm', 'Magic', 'Mortar', 'Cannon']);

export const Weapon = Named('UnitWeapon', 'One attack: damage roll, timing, reach and delivery.', Type.Object({
  source: Type.String({ description: 'Weapon provenance id.' }),
  sound: WeaponSound,
  attackType: AttackKind,
  base: NonNegative('Base damage.'),
  dice: Count('Damage dice.'),
  sides: Count('Sides per die.'),
  cooldown: NonNegative('Seconds between attacks.'),
  attackPoint: NonNegative('Seconds from swing start to the hit/release.'),
  backswing: NonNegative('Recovery seconds after the hit (presentation).'),
  range: NonNegative('Weapon range in metres.'),
  minRange: NonNegative('Minimum range; closer targets make the unit back off.'),
  ranged: Type.Boolean({ description: 'Ranged: terrain line of sight, rear formation rows, projectile/tracer delivery.' }),
  rangeMeasure: RangeMeasure,
  reach: Type.Optional(Reach),
  strikeTolerance: Type.Optional(NonNegative('Extra distance allowed when a scheduled strike resolves.')),
  delivery: WeaponDelivery,
  targeting: WeaponTargeting,
  projectileSpeed: NonNegative('Projectile speed (0 for instant).'),
  flightTime: Type.Optional(FlightTime),
  splash: Type.Optional(Splash),
  targetMask: Type.Array(WeaponTargetFlag, { minItems: 1 }),
  tracer: Type.Boolean({ description: 'Draw an instant tracer for an instant ranged hit.' }),
}, strict));

export const HostWeapons = Named('UnitHostWeapons', 'Tower weapon by host building.', Type.Object({
  town: Weapon,
  harbor: Weapon,
}, strict));

export const AcquisitionRadius = Named('AcquisitionRadius', 'Automatic acquisition radius by the unit owner/order.', Type.Object({
  hostile: NonNegative('Player-owned unit.'),
  neutral: NonNegative('Neutral-owned unit.'),
  hold: NonNegative('Unit holding position.'),
}, strict));

export const Leash = Named('AcquisitionLeash', 'Maximum pursuit distance from the anchor for an autonomous target.', Type.Object({
  hostile: NonNegative('Player-owned unit.'),
  neutral: NonNegative('Neutral-owned unit.'),
}, strict));

export const Acquisition = Named('UnitAcquisition', 'Automatic target acquisition.', Type.Object({
  radius: AcquisitionRadius,
  leash: Type.Optional(Leash),
  queryPadding: NonNegative('Extra spatial query radius (long hulls).'),
  pressureBias: NonNegative('Score added per attacker already on a candidate (spreads melee).'),
  allyAlertRadius: NonNegative('Idle allies this close join against an attacker (0: none).'),
  retaliate: Type.Boolean({ description: 'Turns on the attacker when hit while free.' }),
  measure: RangeMeasure,
  visibility: Visibility,
  tieBreak: TieBreak,
}, strict));

export const Transport = Named('UnitTransport', 'Transport capability (A00V/Sch3).', Type.Object({
  capacity: Count('Cargo slots.'),
  loadRadius: NonNegative('Boarding and unloading radius.'),
  loadLimit: Count('Units selected per boarding order.'),
}, strict));

export const Heal = Named('UnitHeal', 'Ahea autocast heal.', Type.Object({
  amount: NonNegative('Health restored.'),
  range: NonNegative('Heal range.'),
  cooldown: NonNegative('Seconds between casts.'),
  manaCost: NonNegative('Mana per cast.'),
  rescan: NonNegative('Seconds between searches while nobody needs healing (local).'),
  maxVerticalDelta: NonNegative('Maximum height difference to the target.'),
  organicOnly: Type.Boolean({ description: 'Mechanical units are not healed.' }),
}, strict));

export const Roar = Named('UnitRoar', 'Aroa roar autocast.', Type.Object({
  area: NonNegative('Area radius.'),
  duration: NonNegative('Buff duration.'),
  manaCost: NonNegative('Mana per cast.'),
  damageBonus: NonNegative('Rolled damage bonus fraction.'),
  evaluation: NonNegative('Seconds between autocast evaluations (local).'),
}, strict));

export const Mana = Named('UnitMana', 'Mana pool.', Type.Object({
  max: NonNegative('Maximum mana.'),
  initial: NonNegative('Mana on spawn.'),
  regen: NonNegative('Mana per second.'),
}, strict));

export const Capabilities = Named('UnitCapabilities', 'Typed capabilities; rules decide by these, never by type.', Type.Object({
  canCapture: Type.Boolean({ description: 'Can claim a city/harbor circle.' }),
  canGarrison: Type.Boolean({ description: 'Can be bound as a city guardian.' }),
  canEmbark: Type.Boolean({ description: 'Can board a transport.' }),
  harborGuard: Type.Boolean({ description: 'Can be bound as a harbor guard ship.' }),
  transport: Type.Optional(Transport),
  heal: Type.Optional(Heal),
  roar: Type.Optional(Roar),
  mana: Type.Optional(Mana),
}, strict));

export const UnitSilhouette = Enum('UnitSilhouette',
  'Proxy shape for the strategic stand-in. Formation order uses the dense catalog index, never the kind ordinal.',
  ['Infantry', 'Ranged', 'Mounted', 'Siege', 'Marine', 'Hull', 'Structure']);

export const PortraitSource = Enum('PortraitSource',
  'Where the art setup renders this portrait. Model claims the shared prefab (the Mortar cart is still a model portrait). Variant renders the unit view and does not claim a prefab.',
  ['Model', 'Variant']);

export const Presentation = Named('UnitPresentation', 'Model, portrait, attack clip and proxy shape.', Type.Object({
  model: Type.Union([Type.String(), Type.Null()], { description: 'Model prefab name.' }),
  portrait: Type.String({ description: 'Preferred portrait resource name.' }),
  portraitFallback: Type.String({ description: 'Portrait used until the preferred one is rendered.' }),
  portraitSource: PortraitSource,
  attackClip: Type.Union([Type.String(), Type.Null()], { description: 'Attack clip; null for procedural attacks.' }),
  contact: Type.Number({ minimum: 0, maximum: 1, description: 'Normalised clip time of the hit.' }),
  silhouette: UnitSilhouette,
}, strict));

export const Unit = Named('UnitConfig', 'One unit type.', Type.Object({
  id: Type.String({ pattern: '^[A-Z][A-Za-z0-9]*$', description: 'Stable id; never derived from array order.' }),
  names: Names,
  role: Type.Optional(Names),
  source: Source,
  adaptation: Type.Array(Type.String(), { description: 'Local deviations from the source.' }),
  domain: UnitDomain,
  cost: Count('Gold cost.'),
  points: Count('Point value (bounty, reinforcement).'),
  trainSeconds: NonNegative('Training time.'),
  level: Type.Integer({ minimum: 1, description: 'Required building level.' }),
  building: UnitBuilding,
  maxHealth: Type.Number({ exclusiveMinimum: 0, description: 'Maximum health.' }),
  armor: Type.Number({ description: 'Armor value.' }),
  armorType: ArmorKind,
  mechanical: Type.Boolean({ description: 'Mechanical: not healed by organic-only heals.' }),
  canBeAttacked: Type.Boolean({ description: 'False for capturable posts: attack their guardian.' }),
  movement: Movement,
  collision: Type.Optional(Radius),
  body: Type.Optional(Radius),
  hull: Type.Optional(Hull),
  footprint: Type.Optional(Footprint),
  visual: Visual,
  spawnRadius: Type.Optional(NonNegative('Spawn/unload spacing radius.')),
  weapons: Type.Array(Weapon, { maxItems: 1 }),
  hostWeapons: Type.Optional(HostWeapons),
  acquisition: Type.Optional(Acquisition),
  capabilities: Capabilities,
  presentation: Presentation,
}, strict));

export const UnitsFile = Named('UnitsFile', 'units.json root.', Type.Object({
  version: Type.Literal(1),
  units: Type.Array(Unit, { minItems: 1 }),
}, strict));
