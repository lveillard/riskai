# Revisión adversarial Grok 4.7 · Riesgus v0.26

Fecha: 2026-09-22. La revisión usó exclusivamente
`C:\Users\lveil\.grok\bin\grok.exe` con `--model grok-4.7`, herramientas,
web y subagentes desactivados. Los paquetes finales fueron instantáneas acotadas
de código; no se enviaron credenciales. Los JSON íntegros están en
`.tools/grok-riesgus-review/` y el runner reutilizable es
`.tools/grok-riesgus-review/run.py`.

## Disponibilidad y modelo efectivo

El probe mínimo terminó en 9,369 s con `GROK47_AVAILABLE`. Su `modelUsage`
identifica `grok-4.7-build`; no se sustituyó el modelo silenciosamente.

| Perspectiva | Estado | Tiempo | Modelo informado |
| --- | --- | ---: | --- |
| Gameplay/guarnición, extracto final de 65 líneas | `end_turn` | 128,510 s | `grok-4.7-build` |
| UI/onboarding, diff congelado | `end_turn` | 497,065 s | `grok-4.7-build` |
| Worker/rutas, extracto final de 27 líneas más rutas | `end_turn` | 167,587 s | `grok-4.7-build` |

Antes de reducir los paquetes, dos intentos de gameplay agotaron 600,022 s y
600,031 s sin producir JSON, y dos intentos de deploy agotaron 600,083 s y
180,020 s sin producir JSON. Se registran como indisponibilidad para esos
paquetes, no como revisiones completadas. Una primera solicitud UI grande se
canceló para ajustarla al alcance pedido.

## Respuestas finales de Grok

### 1. Gameplay y relevo de guarnición

> **One confirmed defect.** The economy rules and the naval occupy rule are not in the supplied snapshot, so none of those can be confirmed from this review.
>
> **High — guardian handoff commits before the order is accepted.** `BattleCommands.Tick` calls `Valid(command, true)`, which calls `TryReleaseDefenderForOrder` and swaps the guardian before the command switch. `Move`, `AttackMove`, and `Patrol` can still return false; `Attack` and `Follow` do not report failure. A failed/no-op order does not restore the previous guardian. The smallest safe correction is to commit the handoff only with a successful order, or roll it back on failure.

Triage local: el orden es mejorable transaccionalmente, pero el informe no da
una reproducción alcanzable en el flujo normal. La validación y la aplicación
ocurren consecutivamente en el mismo tick; un guardia vivo conserva un
`NavMeshAgent` válido y `SetDestination` acepta también rutas parciales. Si la
aplicación terrestre devolviera falso, el puesto tampoco queda vacío: el relevo
ya está ligado. Se conserva como robustez potencial, no como defecto de
lanzamiento confirmado ni como cambio de regla.

### 2. UI y onboarding

> No confirmed defect in the supplied snapshot.
>
> The checked paths—the setup and HUD copy, the English phrase table, the countdown stage and progress math, compact header and touch-target sizing, and the countdown's `PickingMode.Ignore`—are internally consistent. Apparent income and “60%” tensions are compatible readings of the written rules, and nothing in these files proves a clip, an unreachable control, or a failing UI test.

### 3. Worker y despliegue

> Coverage is limited to the frozen `worker.js` excerpt and the two production routes. Commented helpers were not audited as code.
>
> **High** — `cacheTtlByStatus` `"200-299"` and `response.ok` include 206, and `Range` is forwarded. A partial response can receive a one-year immutable policy. Cache and mark immutable only status 200; give 206 no edge TTL.
>
> **High** — rebuilding `Response` over a body carrying `Content-Encoding: gzip` omits Cloudflare's `encodeBody: "manual"`. The runtime can treat an already encoded body as needing encoding. Set `encodeBody: "manual"` for byte-preserving pass-through.

Triage final contrastado con Cloudflare en producción:

- **Caché parcial: no reproducido.** Sobre una URL nueva con query de auditoría,
  `Range: bytes=0-10` devolvió 206, 11 bytes y MISS. La misma URL, sin Range,
  devolvió 200, 88.998 bytes y HIT. Su SHA-256 coincide con el bundle original:
  Cloudflare almacenó el cuerpo completo, no el fragmento. La prueba contradice
  la reproducción propuesta para esta configuración.
- **Doble compresión: no reproducido.** Los diez archivos descargados desde
  `riesgus.com` coinciden con el manifiesto, incluidos los bytes gzip de los
  bundles. El navegador inicia la partida sin errores. El Worker conserva el
  stream de origen sin leerlo ni transformarlo; no se introdujo un cambio de
  codificación sin un fallo demostrado.

[Evidencia de rango](riesgus-v0.26/range-verification.json) y
[hashes públicos](riesgus-v0.26/public-verification.json). Son verificaciones
de la release actual; no garantizan el comportamiento de futuros cambios.
La revisión reducida de Grok no cubrió el script SSH/Azure completo.

## Verificación local complementaria

- La economía real es `+4` por ronda si queda al menos una ciudad, más `+1`
  por cada ciudad perteneciente a un país completamente controlado. Países
  fragmentados no suman ese segundo componente.
- El relevo terrestre existente es intencional: una orden que desplaza al
  defensor requiere un aliado dentro de 2,0 m y liga al sustituto antes de
  liberar al anterior. `Stop` y `Hold` no lo desplazan.
- `h00W` puede custodiar un puerto vacío; `n008` no. Se actualizaron pruebas
  PlayMode antiguas que todavía afirmaban la regla contraria.
