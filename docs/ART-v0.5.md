# Arte v0.5

La referencia visual de esta iteración es la captura de RiskReforged Rome aportada por el usuario. Se cambia la composición del prototipo: 12 ciudades en tres regiones de cuatro, costa irregular al noroeste, claros delimitados por coníferas y granito al sureste. El mapa mide 144 × 112 unidades; la victoria territorial requiere conservar 9 ciudades durante 20 segundos. Se retiran el río central, los tres puentes y las carreteras rectas del mapa anterior.

La cámara usa perspectiva, FOV vertical 44°, inclinación (pitch) 52° y giro horizontal (yaw) 35°. El zoom virtual parte de 34, con límites 17–44. Un paso de rueda multiplica la distancia por exp(-0,24): aproximadamente 21 % de acercamiento o 27 % de alejamiento. La interpolación usa SmoothDamp de 0,1 s y conserva el terreno bajo el cursor. Flechas y arrastre trabajan respecto a los ejes de la cámara.

La corrección de scroll elimina una segunda división entre 120: Input System 1.14 ya entrega pasos uniformes con UniformAcrossAllPlatforms. La prueba de regresión introduce un evento de dispositivo normalizado y ejecuta el mismo controlador que usa el jugador.

Las instancias de los soldados se escalan a 0,46 respecto a v0.4 y las ciudades/torres a 0,63, con colisiones y picking adaptados. Los retratos mantienen su tamaño. Las etiquetas regionales usan Georgia o Times New Roman, amarillo y sombra; los nombres de ciudad aparecen al seleccionar, pasar el cursor o mantener Alt. El minimapa representa la costa real y el cuadrilátero visible de la cámara.

StrategicAtlas.png contiene hierba fina, tierra y musgo, granito y agujas de conífera. Es una imagen original generada con ImageGen; véase IMAGEGEN-v0.5.md para el prompt exacto. La arquitectura conserva el atlas original de v0.4. Los árboles usan mallas de ramas escalonadas, con sombras, y los personajes siguen usando KayKit CC0. La captura de Warcraft III se utiliza como referencia visual: no se han incorporado sus píxeles ni sus modelos al juego. No se ha portado nueva lógica del mapa de Risk en esta iteración.

El arte sigue siendo provisional: la silueta de los personajes y el detalle ornamental de la interfaz todavía difieren del referente.
