# v0.16 · Órdenes, puertos y reglas de partida

Casa y torre se seleccionan como un mismo puesto. La selección cubre tejados y paredes, y un anillo amplio rodea el compuesto. Las cajas de selección y el doble clic agrupan tropas móviles; seleccionar un defensor individualmente explica que debe permanecer en su círculo. Las órdenes rechazadas muestran el motivo y se registran con identidad de unidad y tick.

Las hogueras aceptan un punto de salida mediante clic derecho y permiten quitarlo desde su panel. Sin salida, sus nuevos soldados mantienen posición junto al campamento. La superposición usa puntos de ciudades y puertos miembros para delimitar el grupo, y los postes entre ciudades de un mismo propietario desaparecen aunque pertenezcan a países diferentes.

## Reglas verificadas

El ingreso por defecto de Europe y New World es cuatro de base más una moneda por ciudad propia, sin exigir un país completo. Con cero ciudades no hay ingreso de ronda. Completar un país da créditos de refuerzo por ronda (`ceil(ciudades/2)`), emitidos de uno en uno cada 500 ms con el máximo regional fuente de cinco puntos por ciudad. Los créditos pendientes pertenecen al país y se conservan entre rondas y cambios de dueño.

Se corrigen la defensa Light y la cadencia del ballestero, el alcance y la cadencia del caballero, la defensa Medium del mortero y el ataque Piercing/defensa Light del sanador. La torre del puesto h00N/h00O usa 45+1d5, alcance 13 y 0,9 s. Contra el ballestero Light de 200 HP normalmente necesita tres impactos acertados; dos máximos consecutivos bastan para matarlo en dos. [Auditoría y campos heredados](RISK-RULES-v0.16.md).

Guard se presenta como **Caballero** y tiene caballo, jinete, armadura, escudo, lanza y telas de bando de geometría original. Se conserva el ID de simulación y la altura fuente del conjunto montado. Las patas leen la velocidad del agente; el render no aplica daño. Los tres reclutas de marina del puerto corresponden a Marine Private, Major y General de los objetos extraídos. Sus modelos son sustitutos propios/CC0 y no reproducen las variantes artísticas de cada mapa.

## Naval y control

La fragata usa 31–45 de daño Normal cada 1,5 s: base 30 del mapa más 1d15 heredado de hdes, confirmado en el SLK de armas del medio propio de TFT.

Los puertos ofrecen reclutamiento de marina con cola terrestre y cola naval separadas. Las fragatas tienen casco largo y aparejo de guerra; el transporte tiene casco ancho, cubierta de carga y otra silueta. Los marcadores de muelle ayudan a encontrar accesos, pero no se atribuye al script una restricción de puerto que no contiene: A00V busca candidatos dentro de 512 unidades nativas (10,24 Unity), y A00X descarga en la posición del barco.

Seleccionar soldados y hacer clic derecho en un transporte prepara un punto costero común. El transporte y las tropas se acercan antes de cargar. Cambiar de selección conserva el embarque. D permite señalar una playa transitable: el transporte navega y descarga al llegar; agua abierta, acantilados y posiciones sin navegación se rechazan. Esta validación de playa es una adaptación del NavMesh, todavía no una importación completa del pathing fuente.

Una fragata junto al embarcadero puede mantener un puerto sin defensor terrestre. La sucesión terrestre conserva prioridad; sin relevo ni barco cercano, el puesto vuelve a neutral. Se usa una sola transición de propiedad, compartida por puerto y ciudad importados, para evitar reembolsos y conversiones repetidos.

La cámara desplaza un 35 % más rápido por defecto con flechas y bordes. Volver del zoom estratégico y centrar un edificio usa ya los límites del zoom solicitado, sin quedarse en la zona anterior. El arrastre secundario tolera más movimiento pequeño antes de considerarse paneo. Mantener Tab muestra los marcadores sin pausar. Los Marines del puerto usan V/B/C; el transporte conserva B/D cuando está seleccionado.

## Presentación y rendimiento

Cuatro Riberas pierde sus columnas repetidas de ciudades, con alturas y claros ajustados. Los bosques originales usan manchas con bordes irregulares y distribución menos uniforme. Los mapas añaden pequeñas manchas de hierba y matorral de geometría propia, agrupadas por zona y sin colisiones ni sombras propias. Se respetan claros de ciudades, círculos, puertos y hogueras. Los materiales de suelo mezclan escalas y orientaciones con ruido continuo; agua y oleaje comparten variación a distintas escalas, sin teselación ni una pasada extra de render.

Las colas terrestres de todas las ciudades y puertos reservan el mismo límite local de población; los refuerzos no pueden consumir esas plazas pagadas. Una hoguera pierde la salida fijada al perder a su dueño, aunque conserva los créditos del país como en la fuente.

La revisión detectó y corrigió un bucle en caminos terrestres parciales, fallos de patrulla que se contaban como órdenes aplicadas y cancelaciones incompletas de embarque. Las dos rondas reales de [Grok 4.6](audits/GROK-v0.16.md) se contrastaron con el código y las reglas fuente.

Las decisiones de las 15 IA se distribuyen por fases deterministas y reutilizan candidatos ofensivos. El HUD recompone sus datos como máximo a 10 Hz. El diagnóstico registra cada 30 s media/máximo de tiempo de fotograma, cambio de heap gestionado, recolecciones y órdenes pendientes/aplicadas/rechazadas. El log de la partida v0.15 no contenía excepciones de gameplay que expliquen por sí solas la incidencia descrita; no se atribuye ese síntoma únicamente al hardware ni se afirma haber reproducido exactamente aquella sesión.

La [validación](VALIDATION-v0.16.md) detalla pruebas y revisión del ejecutable. Quedan pendientes calibración completa de edificios/árboles, pathing de editor, equilibrio prolongado de 16 jugadores, multijugador autoritativo, touch y las plataformas Web/Android.
