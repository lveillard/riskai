# Validación de RiskAI v0.23

La release `20260909T091323Z-966b9a9` está activa en Azure y conserva como rollback `20260909T012000Z-f69b7f0`.

## Cambios de cierre

- El sombreado de capuchas, uniformes, escudos y capas conserva el color canónico WC3 del jugador. Rojo y granate mantienen su diferencia de luminosidad y comparten la misma paleta que tejados y banderas.
- Los ballesteros calculan el alcance contra el borde orientado del casco. Un ballestero en posición defensiva junto al muelle adquiere y daña un transporte sin desplazarse.
- Una compra con varios edificios seleccionados encola una unidad o barco en cada edificio compatible que pueda pagar, recorriendo primero las colas más cortas.
- Los puertos importados conectan visualmente el muelle fuente con el atraque marítimo seguro calculado en ejecución.
- La versión de producto y los directorios de exportación son `0.23.0`, `Windows-v0.23` y `Web-v0.23`.

## Pruebas y builds

- EditMode completo: 132/132.
- Paleta, reglas WC3 y sincronización de ataque: 21/21.
- Regresiones PlayMode de edificios, puertos y combate naval: 20/20.
- Build Windows: 207.040.990 bytes, correcta.
- Build WebGL: correcta.
- La ejecución monolítica de todo PlayMode agotó la memoria del equipo; las áreas modificadas se repitieron en procesos aislados y quedaron verdes.

## WebGL y rendimiento

La carga sostenida usa 900 unidades durante 60,06 s en Edge 152, 1280×800 y una RTX 5080 Laptop:

- 900 unidades vivas y 900 unidades desplazadas.
- 9.000 órdenes; 18.000 aplicaciones; 0 rechazos.
- 49,27 ms de media, 93 ms máximo, 0 frames por encima de 100 ms.
- Resultado del probe: `success=True`.

La comprobación pública a 390×844 y DPR 2 produjo 14 estados responsive, sin errores. Los clics reales de oro y ciudades abrieron sus paneles correspondientes.

## Publicación

- URL: https://riskai-demo.spaincentral.cloudapp.azure.com/
- Release: `/srv/riskai/releases/20260909T091323Z-966b9a9`
- Archivo de despliegue: SHA-256 `b4b2aca71ac8712678b006c3b477371b92bafaa250ab996ef1db3330757506f0`
- Verificación pública: 10/10 archivos coinciden con el manifiesto.

La evidencia reproducible está en `docs/audits/v0.23-release/`.
