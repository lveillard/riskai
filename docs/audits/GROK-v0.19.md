# Grok 4.6 · revisión v0.19

Cuatro revisiones reales con Grok CLI `grok-4.6`, sin herramientas ni navegación. Fueron revisiones de código, no pruebas de Unity, navegador, WebGL ni hardware ARM. Cada hallazgo se contrastó con la fuente actual antes de decidirlo.

## Ronda 1 · UI, viewport y producción

Terminó en 386 s. Detectó problemas reales de propiedad de entrada y presentación: doble dibujo estratégico, cota vertical de etiquetas fijada, módulos UI duplicados, tema runtime adivinado y minimapa IMGUI que también recibía órdenes.

Aplicado:

- IMGUI queda limitado al dibujo; el minimapa usa un overlay UI Toolkit.
- Las etiquetas tácticas y estratégicas siguen `UiViewport.TopPixels`.
- El EventSystem existente desactiva `StandaloneInputModule` antes de usar `InputSystemUIInputModule`.
- El tema se carga desde `Resources/UI/RiskAITheme`.
- El viewport, la cámara y las áreas de entrada comparten los mismos límites.
- Se conservaron las validaciones locales de producción, reembolso y salida.

No reproducido / fuera de la instantánea: una supuesta ausencia del puente Web; el plugin y la actualización mediante `visualViewport` ya estaban presentes. La observación de un futuro jugador local distinto de cero sigue siendo una limitación de la partida local, no un fallo de las reglas actuales.

## Ronda 2 · arbitraje de entrada

Terminó en 497 s. Confirmó carreras reales entre ratón sintético, toque, lápiz, pausa, foco y modal. Se aplicaron correcciones al reconocedor: un único gesto propietario, cancelación consistente, prioridad de punta frente a botón del lápiz, contactos de reemplazo bloqueados y propagación de Shift.

El resultado informado por la ejecución fue 77/77 EditMode. Las regresiones de dispositivos se añadieron junto a las de lógica pura. No se afirma con ello cobertura de navegador ni ARM.

## Ronda 3 · HUD retenido y móvil

Terminó con respuesta efectiva. Sus observaciones que se reprodujeron en fuente o en las primeras capturas se corrigieron:

- El HUD retenido se reconstruye ante tamaño de pantalla, área segura u orientación distintos; no sólo ante cambios de breakpoint.
- La rotación restablece el estado predeterminado del minimapa. En vertical el mapa reserva su propio hueco y la selección, órdenes y producción permanecen disponibles.
- El pie usa la anchura real del minimapa. El encabezado y el pie leen las alturas efectivas tras los límites del viewport.
- Daño, ranking y colas actualizan etiquetas/ranuras en sitio; no recrean el árbol retenido en la cadencia de 10 Hz. Los retratos de cola se cachean; una revisión posterior sustituye el glifo de barco por iconos vectoriales que distinguen fragata y transporte.
- Abrir o cerrar menú/ranking cancela una orden armada. El modal mantiene Volver y Pausa fuera del área desplazable.
- La inspección conserva vida, daño, ataque, alcance y armadura de la unidad enemiga seleccionada.
- Las capturas móviles revelaron un encabezado de frontend superpuesto, tarjetas con desbordamiento horizontal y un botón de inicio fuera de la ventana. El encabezado pasó a ser intrínseco, tarjetas y elecciones compactas ocupan una columna con texto envuelto, y el pie compacto mantiene Iniciar dentro del área segura. La vista muestra `v0.19`.

Se descartaron recomendaciones de cambiar el breakpoint sólo por una heurística de ancho, reescribir el viewport o alterar las reglas de mapa. La sugerencia de un cierre imposible al mantener Tab no se convirtió en cambio: `ScoreboardVisible` es una vista temporal ligada a esa tecla; el modal de menú se cierra de forma explícita.

## Ronda 4 · puente de lápiz del navegador

Terminó en 529 s. Revisó una instantánea del adaptador DOM, el plugin WebGL,
el `Pen` virtual y el controlador compartido. La necesidad del adaptador se
comprobó en el paquete Input System 1.14.2 instalado: su matriz marca Pen como
no compatible con WebGL. No se trasladó la cobertura de Windows al navegador.

Se contrastaron y corrigieron estos casos:

- Una pulsación nueva del mismo identificador tras `pointercancel` puede
  recuperar el lápiz sin exigir un evento de hover que quizá nunca llegue.
- Un contacto iniciado fuera del canvas no genera una pulsación huérfana al
  entrar. Los terminales del documento cubren una captura del puntero fallida.
- Una punta o botón mantenido desde un fotograma con entrada rechazada no se
  convierte en una nueva selección u orden al cerrar el menú o recuperar foco.
- Los bits de botones ajenos al contrato se eliminan en el escritor DOM. Es
  endurecimiento del contrato, no una afirmación de que un navegador normal
  emita ese valor.

La revisión propia añadió foco del canvas explícito, posición del puntero
actual compartida con picking y ausencia de desplazamiento por bordes desde
un lápiz que sólo está en hover. Las pruebas de DOM son independientes de
Unity: no demuestran funcionamiento del reproductor WebGL ni hardware físico.

## Validación disponible

La primera ejecución fue 77 pruebas EditMode y 127 PlayMode; tres fallos iniciales se resolvieron y 22 regresiones dirigidas pasaron. Otra pasada final de 13 casos cubre interfaz y cámara, incluida una nueva regresión del oro al pausar entre ticks. La unión por nombre de caso es de 206 aprobados. La validación visual y de rendimiento posterior se realizó en build nativa: [resultados y límites](../VALIDATION-v0.19.md). Este documento no afirma resultados WebGL, navegador móvil ni ARM.

La ronda del lápiz añade siete casos Unity, aprobados, y repite las cuatro
regresiones de dispositivos nativos: unión actual **213 aprobados**. Node
aprueba 12 casos del puente; Edge aprueba siete comprobaciones DOM con CDP.
Los fixtures restauran las políticas de foco de Input System/Game View al
terminar. La cobertura DOM no contiene el juego Unity ni hardware físico.
