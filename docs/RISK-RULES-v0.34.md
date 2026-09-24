# Reglas de unidades v0.34

Generado por `scripts/generate_unit_rules.py` a partir de `RiskAI/Assets/RiskAI/Resources/Config/units.json`. Ese JSON es la única fuente de los números. Lo que viene de Warcraft 3 está en `source`. Cada entrada de `adaptation` es una desviación local y está marcada abajo como **adaptación**.

Versión del catálogo: 1. Tipos: 20.

La casilla de producción no está en el JSON: `ProductionHotkeys` la calcula (coste, orden estable, tierra antes que mar).

Semántica de órdenes (la matriz completa está en el plan §4.5): atacar y atacar-mover siguen ocupados mientras el blanco vive. Una captura de transporte termina solo cuando la vela y la descarga acaban, aunque el puerto siga hostil o cambie de dueño. Un barco de guerra con captura sobre un puerto hostil que aún tiene guardián vivo sigue combatiendo hasta que el puesto cambia de dueño, igual que v0.33; la cola no avanza mientras tanto.

## Espadachín (Footman)

- Nombre en inglés: Swordsman.
- Rol: Primera línea / Front line.
- **Fuente:** sin rawcode · Adaptación de infantería.
- **Adaptación:** Local fantasy infantry: no source unit; acquisition uses the prototype defaults 7.5/5 (hold: weapon range).
- Dominio Land, edificio City, coste 1, puntos 1, entrenamiento 3 s, nivel 1.
- Vida 200, armadura 2 Heavy, velocidad 5.4, penalización de bosque.
- Arma: 18–21 (17+1d4) cada 1.35 s, alcance 0.9 (BodyEdges, Instant).
- Adquisición: hostil 7.5, neutral 5, mantener posición 0.9 (CenterToApproach, NavMeshRay).
- Capacidades: captura, guarnición, embarca.

## Ballestero (Archer)

- Nombre en inglés: Crossbowman.
- Rol: Ataque a distancia / Ranged attack.
- **Fuente:** h00B · h00B · Rifleman ← hrif.
- **Adaptación:** Presented as a crossbowman: the inherited instant Rifleman weapon becomes a 36 m/s bolt (ua1z=1800).
- Dominio Land, edificio City, coste 1, puntos 1, entrenamiento 1 s, nivel 1.
- Vida 200, armadura 0 Light, velocidad 5.4, penalización de bosque.
- Arma: 17–23 (15+2d4) cada 1.6 s, alcance 8 (CenterToApproach, Missile).
- Adquisición: hostil 12, neutral 12, mantener posición 12 (CenterToApproach, TerrainRay).
- Capacidades: captura, guarnición, embarca.

## Caballero (Knight)

- Nombre en inglés: Knight.
- Rol: Caballería pesada / Heavy cavalry.
- **Fuente:** h00G · h00G · Knight ← hkni.
- Dominio Land, edificio City, coste 5, puntos 5, entrenamiento 1 s, nivel 1.
- Vida 650, armadura 7 Heavy, velocidad 7, penalización de bosque.
- Arma: 39–47 (37+2d5) cada 1.36 s, alcance 2 (BodyEdges, Instant).
- Adquisición: hostil 10, neutral 10, mantener posición 10 (CenterToApproach, NavMeshRay).
- Capacidades: captura, guarnición, embarca.

## Mago (Mage)

- Nombre en inglés: Mage.
- Rol: Daño de área / Area damage.
- **Fuente:** sin rawcode · Adaptación de mago.
- **Adaptación:** Local fantasy unit: missile 25 m/s, 2.4 m splash at 50% on enemy/neutral soldiers, flight 0.15–0.6 s, acquisition 11.
- Dominio Land, edificio City, coste 4, puntos 4, entrenamiento 6 s, nivel 1.
- Vida 250, armadura 1 Unarmored, velocidad 5.4, penalización de bosque.
- Arma: 30–32 (29+1d3) cada 1.6 s, alcance 10 (CenterToApproach, Missile).
- Adquisición: hostil 11, neutral 11, mantener posición 11 (CenterToApproach, TerrainRay).
- Capacidades: captura, guarnición, embarca.

## Mortero (Mortar)

- Nombre en inglés: Mortar.
- Rol: Área a larga distancia / Long-range area damage.
- **Fuente:** h00H · h00H · Mortar ← hmtm.
- Dominio Land, edificio City, coste 3, puntos 3, entrenamiento 1 s, nivel 1.
- Vida 350, armadura 0 Medium, velocidad 4.6, penalización de bosque.
- Arma: 19–31 (18+1d13) cada 3.5 s, alcance 18 (CenterToApproach, Artillery, mínimo 5).
- Adquisición: hostil 18, neutral 18, mantener posición 18 (CenterToApproach, TerrainRay).
- Capacidades: captura, guarnición, embarca.

## Sanador (Medic)

- Nombre en inglés: Healer.
- Rol: Sana aliados · 25 vida · 5 maná / Heals allies · 25 health · 5 mana.
- **Fuente:** h00E · h00E · Medic ← hmpr (220 HP local).
- **Adaptación:** 220 HP instead of the explicit h00E uhpm=250 (v0.30 product decision).
- **Adaptación:** Heal rescan every 0.25 s while nobody needs healing.
- Dominio Land, edificio City, coste 2, puntos 2, entrenamiento 1 s, nivel 1.
- Vida 220, armadura 1 Light, velocidad 5.4, penalización de bosque.
- Arma: 8–9 (7+1d2) cada 2 s, alcance 8 (CenterToApproach, Missile).
- Adquisición: hostil 8, neutral 8, mantener posición 8 (CenterToApproach, TerrainRay).
- Capacidades: captura, guarnición, embarca, cura 25 a 5.

## Marine Private (MarinePrivate)

- Nombre en inglés: Marine Private.
- Rol: Pistolero de puerto / Harbor pistolier.
- **Fuente:** h012 · h012 Marine Private ← hrif.
- Dominio Land, edificio Harbor, coste 1, puntos 1, entrenamiento 1 s, nivel 1.
- Vida 200, armadura 1 Light, velocidad 5.4, penalización de bosque.
- Arma: 18–24 (16+2d4) cada 1.6 s, alcance 6 (CenterToApproach, Instant).
- Adquisición: hostil 12, neutral 12, mantener posición 12 (CenterToApproach, TerrainRay).
- Capacidades: captura, guarnición, embarca.

## Marine Major (MarineMajor)

- Nombre en inglés: Marine Major.
- Rol: Caballería de puerto / Harbor cavalry.
- **Fuente:** h014 · h014 Marine Major ← hkni.
- Dominio Land, edificio Harbor, coste 5, puntos 5, entrenamiento 1 s, nivel 1.
- Vida 650, armadura 6 Heavy, velocidad 5.6, penalización de bosque.
- Arma: 39–47 (37+2d5) cada 1.36 s, alcance 2 (BodyEdges, Instant).
- Adquisición: hostil 10, neutral 10, mantener posición 10 (CenterToApproach, NavMeshRay).
- Capacidades: captura, guarnición, embarca.

## Marine General (MarineGeneral)

- Nombre en inglés: Marine General.
- Rol: Caballería veterana de puerto / Veteran harbor cavalry.
- **Fuente:** h015 · h015 Marine General ← hkni.
- Dominio Land, edificio Harbor, coste 10, puntos 10, entrenamiento 1 s, nivel 1.
- Vida 800, armadura 8 Heavy, velocidad 5.6, penalización de bosque.
- Arma: 66–74 (64+2d5) cada 1.45 s, alcance 2 (BodyEdges, Instant).
- Adquisición: hostil 10, neutral 10, mantener posición 10 (CenterToApproach, NavMeshRay).
- Capacidades: captura, guarnición, embarca.

## Fusilero de élite (EliteRifleman)

- Nombre en inglés: Elite rifleman.
- Rol: Fusilería de élite / Elite marksmanship.
- **Fuente:** h00F · h00F · Elite Rifleman ← hrif.
- Dominio Land, edificio City, coste 6, puntos 6, entrenamiento 1 s, nivel 1.
- Vida 450, armadura 1 Light, velocidad 5.4, penalización de bosque.
- Arma: 38–44 (36+2d4) cada 1 s, alcance 7 (CenterToApproach, Instant).
- Adquisición: hostil 12, neutral 12, mantener posición 12 (CenterToApproach, TerrainRay).
- Capacidades: captura, guarnición, embarca.

## Rugidor (Roarer)

- Nombre en inglés: Roarer.
- Rol: Rugido · +25% daño aliado / Roar · +25% allied damage.
- **Fuente:** h00I · h00I · Roarer ← hmpr.
- **Adaptación:** Aroa autocasts (source: manual) when an ally in the area fights; re-evaluated every 0.5 s.
- Dominio Land, edificio City, coste 4, puntos 4, entrenamiento 1 s, nivel 1.
- Vida 400, armadura 1 Light, velocidad 5.4, penalización de bosque.
- Arma: 30–32 (29+1d3) cada 2 s, alcance 10 (CenterToApproach, Missile).
- Adquisición: hostil 8, neutral 8, mantener posición 8 (CenterToApproach, TerrainRay).
- Capacidades: captura, guarnición, embarca, rugido 14 durante 45 s.

## General (ArmyGeneral)

- Nombre en inglés: General.
- Rol: Caballería de mando · Rugido / Command cavalry · Roar.
- **Fuente:** h00J · h00J · Army General ← hkni.
- **Adaptación:** Aroa autocasts (source: manual) when an ally in the area fights; re-evaluated every 0.5 s.
- Dominio Land, edificio City, coste 10, puntos 10, entrenamiento 1 s, nivel 1.
- Vida 800, armadura 10 Heavy, velocidad 7, penalización de bosque.
- Arma: 57–65 (55+2d5) cada 1.45 s, alcance 2 (BodyEdges, Instant).
- Adquisición: hostil 10, neutral 10, mantener posición 10 (CenterToApproach, NavMeshRay).
- Capacidades: captura, guarnición, embarca, rugido 14 durante 45 s.

## Artillería (Artillery)

- Nombre en inglés: Artillery.
- Rol: Asedio de área a gran distancia / Long-range area siege.
- **Fuente:** h00M · h00M · Artillery ← hmtt.
- Dominio Land, edificio City, coste 15, puntos 15, entrenamiento 1 s, nivel 1.
- Vida 900, armadura 3 Unarmored, velocidad 4, penalización de bosque.
- Arma: 56–68 (55+1d13) cada 3 s, alcance 20 (CenterToApproach, Artillery).
- Adquisición: hostil 20, neutral 20, mantener posición 20 (CenterToApproach, TerrainRay).
- Capacidades: captura, guarnición, embarca.

## Tanque (Tank)

- Nombre en inglés: Tank.
- Rol: Blindado de asedio / Armoured siege vehicle.
- **Fuente:** h01A · h01A · Tank ← hfoo.
- Dominio Land, edificio City, coste 25, puntos 25, entrenamiento 1 s, nivel 1.
- Vida 1500, armadura 9 Fortified, velocidad 5.2, penalización de bosque.
- Arma: 81–91 (80+1d11) cada 1.8 s, alcance 10 (CenterToApproach, MissileSplash).
- Adquisición: hostil 10, neutral 10, mantener posición 10 (CenterToApproach, TerrainRay).
- Capacidades: captura, guarnición, embarca.

## Fragata (Frigate)

- Nombre en inglés: Frigate.
- **Fuente:** h00W · sin base.
- Dominio Sea, edificio Harbor, coste 5, puntos 5, entrenamiento 1 s, nivel 1.
- Vida 400, armadura 6 Light, velocidad 6.8.
- Arma: 31–45 (30+1d15) cada 1.5 s, alcance 20 (ToHull, MissileSplash).
- Adquisición: hostil 20, neutral 20, mantener posición 20 (ToHull, TerrainRay).
- Capacidades: captura, guardia de puerto.

## Transporte (Transport)

- Nombre en inglés: Transporte.
- **Fuente:** n008 · sin base.
- Dominio Sea, edificio Harbor, coste 2, puntos 2, entrenamiento 1 s, nivel 1.
- Vida 300, armadura 0 Heavy, velocidad 6.8.
- Capacidades: transporte 10 plazas, radio 10.24.

## Buque de guerra (Warship)

- Nombre en inglés: Warship.
- **Fuente:** h00U · sin base.
- Dominio Sea, edificio Harbor, coste 20, puntos 20, entrenamiento 1 s, nivel 1.
- Vida 1250, armadura 10 Light, velocidad 9.
- Arma: 91–105 (90+1d15) cada 1.5 s, alcance 30 (ToHull, MissileSplash).
- Adquisición: hostil 30, neutral 30, mantener posición 30 (ToHull, TerrainRay).
- Capacidades: captura, guardia de puerto.

## Acorazado (Battleship)

- Nombre en inglés: Battleship.
- **Fuente:** h001 · sin base.
- Dominio Sea, edificio Harbor, coste 45, puntos 45, entrenamiento 1 s, nivel 1.
- Vida 2350, armadura 20 Light, velocidad 6.6.
- Arma: 131–145 (130+1d15) cada 1.4 s, alcance 30 (ToHull, MissileSplash).
- Adquisición: hostil 30, neutral 30, mantener posición 30 (ToHull, TerrainRay).
- Capacidades: captura, guardia de puerto.

## Transporte blindado (ArmoredTransport)

- Nombre en inglés: Armoured transport.
- **Fuente:** n007 · sin base.
- Dominio Sea, edificio Harbor, coste 6, puntos 6, entrenamiento 1 s, nivel 1.
- Vida 300, armadura 30 Heavy, velocidad 7.4.
- Capacidades: transporte 10 plazas, radio 10.24.

## Torre (Tower)

- Nombre en inglés: Tower.
- **Fuente:** o000 · o000 · Bunker.
- Nota de fuente: h00N/h00O City Post.
- **Adaptación:** Health from the o000 bunker; attack, Divine armor 3 and delivery from the h00N/h00O city post, weapon by host building.
- Dominio Static, edificio None, coste 0, puntos 0, entrenamiento 0 s, nivel 1.
- Vida 550, armadura 3 Divine, velocidad 0.
- No se puede atacar: se ataca a su guardián o se captura el edificio.
- Arma de ciudad: 46–50 (45+1d5) cada 0.9 s, alcance 13 (CenterToCenter, Missile).
- Arma de puerto: 46–50 (45+1d5) cada 0.9 s, alcance 13 (CenterToCenter, Missile).
- Adquisición: hostil 13, neutral 13, mantener posición 13 (CenterToCenter, TerrainRay).
