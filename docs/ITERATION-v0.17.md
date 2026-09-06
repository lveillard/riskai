# v0.17 · Respuesta, navegación y legibilidad

La cola naval admite cinco encargos y muestra sus iconos y progreso. El anillo
grande de embarque aparece al seleccionar el puerto; el círculo pequeño del
defensor conserva su función. La reunión inicial de soldados queda junto a la
entrada y no depende del color del propietario. Un punto de salida elegido por
el jugador sigue teniendo prioridad.

Cada edificio muestra un pequeño martillo y estandarte animados mientras
recluta. Los puertos importados comparten este indicador con su ciudad. La
cámara de Europe y New World usa una inclinación de 55 grados, igual que los
mapas originales. Las órdenes tienen una confirmación circular que se contrae
y desvanece reutilizando el mismo objeto.

Las copas se indexan al cargar el mapa. Una unidad que puede estar oculta tras
ellas muestra su barra de vida incluso con salud completa, con comprobación
a 10 Hz. El bosque denso deja pasar y reduce la velocidad un 18 %: es una
adaptación local y el planificador aún no calcula rutas según ese coste.
Rocas y edificios conservan sus obstáculos.

Las orillas importadas mezclan arena y roca mediante una banda precalculada
y ruido del material. Esto mejora el color de la transición, pero la silueta
geométrica todavía sigue las celdas W3E. Saetas, orbes y proyectiles de mortero
tienen formas e impactos distintos dentro de sus pools. La presentación sigue
sin decidir el daño.

## Trabajo de rendimiento

Se eliminan arrays temporales del picking y de los marcos del HUD, se reutilizan
estilos y se calcula la presencia de cada jugador una vez por tick para victoria.
Las búsquedas navales reutilizan un grafo de pasos con espacio para el casco,
preparado al cargar. Sus componentes permiten rechazar rutas entre masas de
agua desconectadas sin recorrer todo el mapa. Un trayecto recto seguro evita A*.
Los segmentos devueltos siguen comprobándose y las diagonales no atraviesan
esquinas de costa.

Cada IA naval conserva una decisión cada 18 segundos, distribuida por jugador.
No vuelve a calcular una ruta hacia el mismo puerto mientras ya navega hacia
él. El suavizado de trayectorias aún es síncrono y su coste se mide por separado;
no se presenta esta mejora como navegación apta para miles de barcos.

La navegación terrestre dispone de un presupuesto explícito de 500 expansiones
asíncronas por frame en esta build de escritorio. Unity documenta un valor
predeterminado de 100 y el intercambio entre latencia de ruta y coste por frame;
el cambio se comprueba en el ejecutable y necesitará medición propia en tablet.
[API de Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AI.NavMesh-pathfindingIterationsPerFrame.html).
Combate, seguimiento y regreso al puesto conservan una petición que aún se
está resolviendo y una ruta que ya llega al destino solicitado. Las órdenes
explícitas del jugador pueden sustituirla inmediatamente.

Las [dos revisiones de Grok 4.6](audits/GROK-v0.17.md) se contrastaron con el
código y pruebas. Se corrigieron el rodeo por una misma celda, falsos mensajes
de éxito al embarcar, el margen de llegada para descargar y la recuperación
de rutas que dejan de avanzar. La sonda distingue un escenario de prueba
inválido de una orden que falla.

El diagnóstico distingue fases de simulación, IA terrestre/naval y búsquedas
de mar. Registra latencia desde la orden aceptada hasta aplicación y primer
movimiento elegible, y separa ventanas de foco y pausa. Se puede leer la partida
sin cerrarla mediante `python scripts/observe_runtime.py --follow`.
[Uso y límites](OBSERVABILITY.md) · [Resultados](VALIDATION-v0.17.md).

## Reglas y próximos cortes

La [auditoría de counters](audits/COUNTERS-v0.17.md) comprueba directamente
`war3mapMisc.txt` en Europe y New World: ambos usan la misma matriz. Se corrige
el comentario sobre el orden de columnas, sin alterar sus valores. Los datos
heredados y las unidades locales siguen identificados como tales.

No se cambia el contrato de comandos de infantería ni se introduce dependencia
del dispositivo en las reglas. Input Actions/touch, compras y barcos en el
protocolo de órdenes, servidor autoritativo, Web/Android y la migración del HUD
siguen en [TODO.md](../TODO.md). El siguiente trabajo visual incluye geometría
de costa y animaciones propias de mortero y Marines.
