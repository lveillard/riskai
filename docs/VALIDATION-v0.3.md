# Validación v0.3 — 5 de septiembre de 2026

- Unity 6000.3.23f1, Windows x64, URP, Input System.
- EditMode: 5/5 pruebas aprobadas. Informe: `TestResults/editmode-v03.xml`.
- PlayMode final: 13/13 aprobadas, 62,64 segundos. Informe: `TestResults/playmode-v03-final.xml`.
- Casos de juego: navegación por puentes, compras y pausa, combate/captura, detener frente a mantener posición, ataque en movimiento, cámara suave, cancelación/reembolso, mejora de ciudad, defensa/reconstrucción, asedio desde el perímetro, clic fuera del collider, límite de población contando otras ciudades, orden de formación y punto de reunión.
- La compilación para jugar utiliza `BuildOptions.None`; no incluye el reproductor de desarrollo.

Compilación Windows completada: `RISKAI_BUILD_OK`, 129.633.438 bytes, registro `RiskAI/Logs/build-v03-final.log`. El ejecutable arrancó con Direct3D 12; el registro `TestResults/player-v03-final.log` no contiene excepciones de C# ni errores de shaders en la comprobación de arranque.

Se observaron la vista general, ciudades, torres, terreno y panel inicial. La revisión interactiva final de compras, iconos y zoom está pendiente: un aviso de Windows Security/firewall cubre el juego y necesita que el usuario lo cierre. La automatización no ha modificado permisos del firewall. Los modelos de retrato Knight y Mage también se revisaron en sus PNG generados por Unity.
