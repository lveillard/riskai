# Herencia WC3 frente a Reforged actual

Consulta: **8 de septiembre de 2026**. El servicio de versiones de Blizzard
publicaba **2.0.4.23745** para EU/US/KR. El mapa declara **2.0.2.22796**, formato
W3I 31 y `game_data_set = 0` (offset `0xb2`). Son dos versiones distintas:
esta auditoría contrasta datos actuales, no certifica una partida de 2.0.2.

**No es correcto reemplazar la herencia por las tablas raíz actuales.** El
contenedor `war3.w3mod` incluye `_balance/custom_v0.w3mod`, `custom_v1.w3mod`
y `melee_v0.w3mod`. Se han extraído sus tablas y comparado por separado. El
selector del motor que vincula W3I dataset 0, modo de partida y estas capas
sigue sin prueba. Por ello, no se ha cambiado el runtime ni el roster.

## Resultado

69 objetos × 68 campos = 4692 observaciones. Las modificaciones explícitas del
mapa siempre prevalecen. La comparación conserva valores crudos y rutas/líneas
de las cuatro variantes en el [derivado JSON](../../data/derived/reforged-latest-inheritance.json).

| Clasificación | Campos | Interpretación |
|---|---:|---|
| Explícitos W3U verificados | 576 | Valor del mapa; no sustituir por Blizzard |
| Heredados invariantes entre las cuatro variantes | 1180 | Valor actual coincide independientemente de estas capas |
| Dependientes de selección de capa o presencia de fila | 570 | Conservar candidatos; falta selección efectiva del motor |
| Nulos o sin dato resoluble | 2366 | No convertir automáticamente a cero; incluye armas no usadas y campos ausentes |

Ninguno de los 1180 valores invariantes difiere del candidato histórico TFT.
Esto **no** significa que todos los campos coincidan: las diferencias relevantes
se concentran precisamente entre las variantes pendientes de selección.

| Objeto / campo heredado | Raíz actual | custom_v0 | custom_v1 | melee_v0 |
|---|---:|---:|---:|---:|
| h00B Rifleman, `ua1c` intervalo de ataque | 1.4 | 1.5 | 1.5 | 1.5 |
| h00B Rifleman, `udty` armadura (token SLK) | medium | small | medium | small |
| h00G Knight, `ua1c` | 1.4 | 1.36 | 1.5 | 1.36 |
| h00J Army General, `uhpm` | 885 | 800 | 800 | 800 |
| h00T / h015 Marine General, `uhpm` | 885 | 800 | 800 | 800 |
| h00I Roarer, `ua1t` tipo de ataque | magic | pierce | magic | magic |
| h00H Mortar, `udty` | large | medium | large | medium |
| h00U Warship A, `ua1c` | 1.5 | sin fila | 1.5 | sin fila |

Los tokens de armadura son los de la tabla, no nombres de categorías Unity.
«Sin fila» significa ausencia en el archivo alternativo inspeccionado; no
demuestra que el motor carezca de la unidad. La comparación sustituye archivos
homónimos para inspeccionar cada variante y **no inventa** una fusión de filas
ni reglas de fallback del motor. Los casos afectados quedan pendientes.

Ejemplos que sí manda el mapa: Tank h01A tiene 1500 HP, daño base 80, alcance
500 y ataque siege; Artillery h00M tiene 900 HP, daño base 55, alcance 1000 e
intervalo 3.0. Battleship SS h001 tiene 2350 HP y alcance 1500. El intervalo
explícito de h00J/h00T/h015 es aproximadamente 1.45 (float32); no debe
reemplazarse por el intervalo heredado de Knight.

## Procedencia y verificación

- Versiones: <https://us.version.battle.net/w3/versions>.
- Build config: `9a94ff7d25781db6c09190cd69d52458`.
- CDN config: `bb855f9558e73ed8da351212f96f4ed9`.
- URL de configs: `https://us.cdn.blizzard.com/tpr/war3/config/XX/YY/HASH`,
  donde XX/YY son los primeros dos pares del hash.
- TVFS de war3mod: Ekey `7db8d84e0930ea20c88faf2f33ec2dcb`; SHA-256 decodificado
  `f0cf291a0258134388cd94d7666761376072f10bf8bb99abf0698cc2b12148a6`.
- Los 112 archivos de evidencia suman 12 697 599 bytes decodificados. Se
  descargaron tablas pequeñas y directorios, sin instalar el juego.

El JSON incluye URL exacta, rango HTTP cuando procede, Ekey, tamaño y SHA-256
de cada archivo. `remote_roundtrip_verified=true` identifica archivos clave
releídos del CDN y contrastados byte a byte con la caché: los tres TVFS de
balance y las tablas disponibles UnitBalance, UnitWeapons, UnitData y
UnitMetaData. Se verificó MD5 de la cabecera BLTE (o del objeto entero para
BLTE sin cabecera de chunks), MD5 de cada chunk y tamaño decodificado.
Los restantes archivos conservan SHA-256 local y procedencia de descarga;
el indicador no los presenta como una nueva relectura remota.

## Reproducción

Desde la raíz del repositorio:

```powershell
python scripts/audit_latest_inheritance.py --verify-cdn
```

El script está fijado a la build auditada; una ejecución posterior no afirma
que 2.0.4.23745 siga siendo la última. Para actualizar hay que consultar de
nuevo versiones y resolver sus nuevos manifests.

Prerrequisitos de esta ejecución: Python estándar, los parsers existentes del
repositorio, `data/derived/reforged-source-combat.json` y la caché privada en
`.tools` (no versionada): configs bajo
`.tools/v22-validation/reforged-latest/.tools_<HASH>.bin`, TVFS decodificado
`.tools_war3mod.tvfs`, índices `.tools/cdn-current/indexes/<ARCHIVE>.index`
y tablas raíz bajo `.tools/cdn-current/2.0.4.23745/`. No es un instalador
automático para un clon vacío. Las URLs y hashes del derivado permiten
recuperar los archivos de evidencia: para cada URL con rango, pedir ese rango,
decodificar BLTE con `blte()` del script y comparar SHA-256. El TVFS war3mod
se obtiene de `https://us.cdn.blizzard.com/tpr/war3/data/7d/b8/7db8d84e0930ea20c88faf2f33ec2dcb`.
Los índices se obtienen de `data/XX/YY/<ARCHIVE>.index`; la lista ARCHIVE está
en `archives` de la CDN config. Su formato y TVFS se contrastaron con CascLib
`src/CascRootFile_TVFS.cpp` y `src/CascIndexFiles.cpp`.

Pendientes: probar selección/fallback efectivos del motor, contrastar también
la build histórica 2.0.2 si se requiere fidelidad a esa versión, y validar
habilidades/upgrades/constantes de combate antes de declarar paridad completa.
Este informe abarca campos de unidades; haber extraído otras tablas no
certifica automáticamente su semántica. Las ocho altas propuestas y las
identidades Marine están en [SOURCE-ROSTER-NEXT.md](SOURCE-ROSTER-NEXT.md).
