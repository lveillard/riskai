# Riesgus v0.29.1 — altura de la torre integrada

22 de septiembre de 2026. Ajuste visual sobre v0.29, **PUBLICADO** en
https://riesgus.com como `20260922T194407Z-tower-v0291`.

Las torres integradas de Europe y New World crecen 0,45 m (aproximadamente
un 10 % de altura total). Se alarga el cuerpo y se elevan galería, almenas,
tejado, heráldica y andamio sin estirar sus detalles. Base y anchura permanecen
iguales, con las mismas mallas/materiales y 10 renderers. Las torres separadas
de los mapas personalizados no cambian.

Rótulo y zona clicable acompañan la altura visual. Los puntos de adquisición
y disparo, daño, alcance, colisiones y navegación quedan intactos.

Validación realizada:

- Previews de ciudad/puerto revisadas en `Captures/tower-v0291`; ejecución
  correcta en `RiskAI/Logs/tower-v0291.log`.
- 32/32 casos PlayMode aprobados, sin fallos ni omitidos:
  `TestResults/v0291-playmode.xml`. Clases `ImportedMapGameplayTests`,
  `BuildingSelectionTests`, `TowerCombatTests` y `BattleHudTests`.
  Incluye ambos mapas importados y la altura visual independiente del combate.
- No se repite el benchmark de recursos: no se añaden piezas, texturas ni
  pases. Esto no equivale a demostrar tiempos de GPU idénticos.

Windows compilado en `Builds/Windows-v0.29.1/RiskAI.exe`:
`RISKAI_BUILD_OK: 212143713 bytes`. Web compilada en `Builds/Web-v0.29.1`:
`RISKAI_WEB_BUILD_OK: 57649794 bytes`.

World Web local, 390×844/DPR 2: `success=true`, sin errores, 16 capturas,
65,58 s; `Captures/v0291-world-final/result.json`. Puerto revisado visualmente.
Es emulación móvil, no hardware físico. El rótulo largo conserva el recorte
preexistente de v0.29, sin cambiar su disposición respecto a la parte superior.

Recibo `activated=true` y verificación pública 10/10 hashes en
`.deploy/20260922T194407Z-tower-v0291/{receipt,public-verification}.json`.
Se conserva `20260922T191712Z-water-v029` para rollback. IP autorizada
`176.223.53.50` sin cambios; no se modifican Cloudflare ni reglas de acceso.

Partida pública Europe 1600×900/DPR 1: `success=true`, sin errores,
17 capturas, 75,33 s; `Captures/v0291-public-europe-final/result.json`.
Servidor local de comprobación cerrado al terminar.

## Verificación previa a integrar en Git

Antes de integrar todo lo acumulado en la PR #3 se repite EditMode: 178/178
aprobados (`TestResults/merge-v0291-editmode.xml`). También pasan 26 tests
Python, los 17 checks del puente pen/rueda y los checks del Worker. El comando
Cloudflare `Check` usa ahora la build vigente y no despliega nada.

Las comprobaciones PlayMode y Web de esta versión son las registradas arriba;
no se ha modificado código de juego después. La PR no tiene checks de GitHub
Actions configurados. La búsqueda de patrones de credenciales no encuentra
coincidencias; builds, logs, capturas temporales, `.deploy`, estado de Wrangler
y variables locales quedan excluidos del índice.
