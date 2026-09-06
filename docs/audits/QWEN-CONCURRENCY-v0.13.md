# Qwen3.8: prueba de concurrencia por Grok CLI

Medición local del 6 de septiembre de 2026. Modelo solicitado `qwen38`, identificado en respuestas como `Qwen/Qwen3.8-27B-FP8`. La tarea de cada trabajador consistió en extraer tres hechos y líneas de un único archivo C# suministrado. Sin acceso a secretos, archivos de disco ni arte. Paquetes, resultados brutos y métricas quedan ignorados en `.tools/qwen-facts-benchmark/`.

| Simultáneos | Respuestas completas | Mediana por tarea (s) | Fin de tanda (s) |
| ---: | ---: | ---: | ---: |
| 3 | 3/3 | 27.1 | 41.0 |
| 4 | 4/4 | 15.1 | 37.1 |
| 5 | 5/5 | 40.4 | 58.1 |
| 6 | 6/6 | 36.5 | 61.6 |
| 7 | 7/7 | 38.2 | 45.6 |
| 8 | 8/8 | 35.1 | 74.2 |
| 9 | 9/9 | 44.4 | 60.7 |
| 10 | 9/10 | 55.9 | 120.8 |

**Resultado:** 51 de 52 tareas devolvieron texto con `end_turn`. Una de las diez simultáneas agotó el límite de 120 s. Esto mide disponibilidad y duración, no corrección factual. No es una prueba de rendimiento del juego ni una garantía de cuota/concurrencia del servicio.

Las tandas usaron archivos diferentes y tamaños variables, sin réplicas estadísticas. Además Unity ejecutaba pruebas en la misma máquina; parte del tiempo puede incluir carga local del CLI, red o colas del servicio. No permite afirmar que cuatro sea siempre más rápido que tres, ni que el único timeout se deba a la concurrencia.

**Uso adoptado:** 4–6 extracciones pequeñas por tanda para revisiones ordinarias, menos cuando Unity esté consumiendo recursos. Se llegó a diez para comprobar el límite solicitado, sin una mejora clara que compense aumentar por defecto. Mantener evidencias de archivo/línea y revisar cada hallazgo antes de tocar reglas.

Los ensayos previos de revisión semántica eran peores: 3/3 respuestas en la primera tanda y 2/4 en la siguiente; aparecían intentos de herramientas y falsos positivos. Cambiar a extracción literal, `--no-plan --verbatim`, paquetes de un archivo y herramientas deshabilitadas produjo las tandas de esta tabla. No se atribuyen esos fallos anteriores a falta de capacidad del servicio.

Grok4.6 se intentó para revisión adversarial, pero no terminó: se canceló un intento prolongado y un segundo paquete pequeño agotó 180 s. Esos intentos **no** cuentan como revisión completada. El contraste final de código y las pruebas Unity lo realizó el integrador.

Patrón local que produjo las extracciones acotadas:

```powershell
grok --model qwen38 --tools none --no-subagents --disable-web-search --no-plan --verbatim --max-turns 4 --system-prompt-override "Extract literal facts from supplied text. No tools, no plans." --prompt-file packet.txt --output-format json
```

Cada paquete contiene un archivo o una función completa con números de línea y una pregunta específica. El proceso padre impone 120 s por tarea y registra `stopReason`, texto, duración y uso. Incluir también las funciones que definen condiciones externas: la posterior tanda de JASS mostró que omitirlas puede convertir una excepción de modo en un supuesto valor predeterminado.
