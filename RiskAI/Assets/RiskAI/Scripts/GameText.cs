using System;
using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    public enum GameLanguage { English, Spanish }

    /// <summary>Single presentation boundary for the two supported UI languages.</summary>
    public static class GameText
    {
        public static GameLanguage Language { get; private set; } = GameLanguage.English;
        public static bool IsSpanish => Language == GameLanguage.Spanish;
        public static string SwitchLabel => IsSpanish ? "EN" : "ES";

        const string PreferenceKey = "riskai.language";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => Language = GameLanguage.English;

        // Players start in their own language: an explicit toggle (PlayerPrefs) wins,
        // then the browser (navigator.language) or OS language; Spanish for es-*, else English.
        // Editor and batch runs keep the deterministic English default that tests assume.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void DetectOnLoad()
        {
            if (Application.isEditor || Application.isBatchMode) return;
            Language = Initial(StoredPreference(), PlatformPresentation.PrefersSpanish);
        }

        /// <summary>Resolution order for the starting language. A stored choice beats detection.</summary>
        public static GameLanguage Initial(GameLanguage? stored, bool systemPrefersSpanish) =>
            stored ?? (systemPrefersSpanish ? GameLanguage.Spanish : GameLanguage.English);

        static GameLanguage? StoredPreference()
        {
            try
            {
                if (!PlayerPrefs.HasKey(PreferenceKey)) return null;
                return PlayerPrefs.GetInt(PreferenceKey) == (int)GameLanguage.Spanish ? GameLanguage.Spanish : GameLanguage.English;
            }
            catch (Exception) { return null; }
        }

        /// <summary>The player's explicit choice: applied and remembered for later sessions.</summary>
        public static void Toggle()
        {
            Language = IsSpanish ? GameLanguage.English : GameLanguage.Spanish;
            try { PlayerPrefs.SetInt(PreferenceKey, (int)Language); PlayerPrefs.Save(); }
            catch (Exception) { }
        }
        /// <summary>Sets the language for this session only (tests, probes); not persisted.</summary>
        public static void Set(GameLanguage language) => Language = language;

        static readonly Dictionary<string,string> Exact = new Dictionary<string,string>
        {
            ["RIESGUS"]="RIESGUS", ["DOMINIOS"]="DOMINIONS", ["CONQUISTA"]="CONQUEST", ["PAUSADO"]="PAUSED",
            ["PAUSA"]="PAUSE", ["Pausa"]="Pause", ["CONTINUAR"]="RESUME", ["Continuar"]="Resume",
            ["VOLVER"]="BACK", ["Menú"]="Menu", ["Mapa"]="Map", ["Ranking"]="Ranking",
            ["Partida"]="Match", ["Controles"]="Controls", ["Sonido"]="Sound", ["Cámara"]="Camera", ["Idioma"]="Language",
            ["PARTIDA"]="MATCH", ["SONIDO"]="SOUND", ["CÁMARA"]="CAMERA", ["CONTROLES"]="CONTROLS", ["IDIOMA"]="LANGUAGE",
            ["Volumen general"]="Master volume", ["Efectos"]="Effects", ["Música"]="Music", ["Minimapa"]="Minimap",
            ["Activado"]="On", ["Desactivado"]="Off", ["Todos"]="All", ["Cola"]="Queue", ["Astillero"]="Shipyard", ["Ciudades"]="Cities", ["VICTORIA"]="VICTORY", ["DERROTA"]="DEFEAT",
            ["COMENZAR LA CONQUISTA"]="START CONQUEST", ["PREPARANDO LA CONQUISTA"]="PREPARING CONQUEST",
            ["ELEGIDO"]="SELECTED", ["NUEVA SEMILLA"]="NEW SEED", ["SEMILLA"]="SEED",
            ["REPARTO INICIAL"]="STARTING LAYOUT", ["DIFICULTAD DE IA"]="AI DIFFICULTY",
            ["RELIEVE IMPORTADO"]="IMPORTED RELIEF", ["NUEVA PARTIDA"]="NEW MATCH",
            ["SALA DE GUERRA · CONFIGURA TU CAMPAÑA"]="WAR ROOM · CONFIGURE YOUR CAMPAIGN",
            ["RIESGUS · DESPLIEGUE"]="RIESGUS · DEPLOYMENT",
            ["NUEVA PARTIDA · ELEGIR MAPA"]="NEW MATCH · CHOOSE MAP", ["CENTRAR MAPA"]="CENTER MAP",
            ["RESTABLECER CÁMARA"]="RESET CAMERA", ["OCULTAR MAPA TÁCTICO"]="HIDE TACTICAL MAP",
            ["MOSTRAR MAPA TÁCTICO"]="SHOW TACTICAL MAP", ["DESGLOSE DEL ORO"]="GOLD BREAKDOWN",
            ["UNIDADES"]="UNITS", ["CLASIFICACIÓN · CIUDADES"]="RANKING · CITIES",
            ["ÓRDENES DE HOGUERA"]="CAMP ORDERS", ["BORRAR SALIDA"]="CLEAR RALLY",
            ["Mover"]="Move", ["Atacar"]="Attack", ["Patrullar"]="Patrol", ["Detener"]="Stop",
            ["Mantener"]="Hold", ["Centrar"]="Focus", ["Embarcar"]="Board", ["Desembarcar"]="Unload",
            ["Puerto"]="Harbor",
            ["maná"]="mana", ["rugido +25%"]="roar +25%",
            ["· carga "]="· cargo ", [" daño · "]=" damage · ",
            ["Preparado"]="Ready", ["Moviendo"]="Moving", ["En combate"]="In combat", ["Patrullando"]="Patrolling",
            ["Siguiendo"]="Following", ["Manteniendo posición"]="Holding position", ["En puerto"]="In harbor",
            ["Navegando"]="Sailing", ["Neutral"]="Neutral", ["Tú"]="You",
            ["Rojo"]="Red", ["Azul"]="Blue", ["Turquesa"]="Teal", ["Violeta"]="Purple",
            ["Amarillo"]="Yellow", ["Naranja"]="Orange", ["Verde"]="Green", ["Rosa"]="Pink",
            ["Gris"]="Gray", ["Azul claro"]="Light Blue", ["Verde oscuro"]="Dark Green",
            ["Marrón"]="Brown", ["Burdeos"]="Burgundy", ["Azul marino"]="Navy", ["Cian"]="Cyan", ["Magenta"]="Magenta",
            ["Las Marcas"]="The Marches", ["Cuatro Riberas"]="Four Riverlands",
            ["LAS MARCAS"]="THE MARCHES", ["CUATRO RIBERAS"]="FOUR RIVERLANDS",
            ["NEW WORLD · EUROPA Y AMÉRICA"]="NEW WORLD · EUROPE AND AMERICA",
            ["Elige tu campo de batalla"]="Choose a battlefield", ["Prepara la expedición"]="Prepare the expedition",
            ["Ciudades al azar"]="Random cities", ["Países iniciales"]="Starting countries", ["Posiciones fijas"]="Fixed positions",
            ["Relajada · tácticas sencillas"]="Relaxed · simple tactics", ["Estándar · mayor coordinación"]="Standard · coordinated tactics",
            ["Difícil · oleadas coordinadas"]="Hard · coordinated waves", ["Relajado"]="Relaxed", ["Estándar"]="Standard", ["Difícil"]="Hard"
        };

        static readonly KeyValuePair<string,string>[] Phrases = {
            // Top bar, command card and controls table (BattleHud.CommandCard). Listed first so
            // generic word pairs below (Cancelar, oro...) cannot pre-empt these sentences.
            Pair("Desglose del oro y del próximo ingreso","Gold breakdown and next income"),
            // Quick bar, ranking board, menu sections and chat recipients (BattleHud.Overlay, BattleMenu, Feedback).
            Pair("Efectos de sonido (F7)","Sound effects (F7)"), Pair("Música (F8)","Music (F8)"), Pair("Clasificación (Tab)","Ranking (Tab)"),
            Pair("Minimapa (F9)","Minimap (F9)"), Pair("Chat (Intro)","Chat (Enter)"), Pair("Efectos silenciados","Effects muted"), Pair("Efectos activados","Effects on"),
            Pair("Unidades totales, incluidos defensores y barcos","Total units, including defenders and ships"), Pair("Ingreso por ronda","Income per round"),
            Pair("Países completos","Completed countries"), Pair("Ciudades controladas / total · abrir clasificación","Controlled cities / total · open ranking"), Pair("CLASIFICACIÓN","RANKING"), Pair("Cerrar (Esc)","Close (Esc)"),
            Pair("Volumen de la música","Music volume"), Pair("Atajos: F7 efectos · F8 música.","Shortcuts: F7 effects · F8 music."),
            Pair("Velocidad de la cámara","Camera speed"), Pair("Paneo en los bordes","Edge panning"), Pair("Temblor de cámara","Camera shake"),
            Pair("La elección se guarda para las próximas partidas.","Your choice is kept for future matches."),
            Pair("Destinatario · Tab cambia · Mayús+Intro envía a todos","Recipient · Tab cycles · Shift+Enter sends to all"),
            Pair("No hay ningún jugador con ese nombre o color.","No player has that name or colour."),
            Pair("Efectos de sonido · música · minimapa (también en la barra rápida)","Sound effects · music · minimap (also in the quick bar)"),
            Pair("Chat al destinatario elegido · enviar a todos","Chat to the chosen recipient · send to all"),
            Pair("Cambiar de destinatario · mensaje privado (p. ej. /w azul hola, /azul hola)","Change recipient · private message (e.g. /w blue hi, /blue hi)"),
            Pair("Tab en el chat · /w color","Tab in chat · /w colour"), Pair("Intro · Mayús+Intro","Enter · Shift+Enter"),
            Pair("¡Has perdido ","You lost "), Pair("País roto: sin oro ni refuerzos de ","Country broken: no gold or reinforcements from "),
            Pair("Oro y refuerzos de ","Gold and reinforcements from "), Pair(" cada ronda"," every round"),
            Pair("Para: ","To: "), Pair("Para ","To "), Pair(" → Todos"," → All"),
            Pair("Sin guarnición · un enemigo en el círculo la conquista","No garrison · an enemy in the circle captures it"),
            Pair("Guarnición · ","Garrison · "), Pair("Torre en construcción","Tower under construction"), Pair(" · sin guarnición no dispara"," · no garrison, it does not fire"),
            Pair("Torre destruida","Tower destroyed"), Pair("Sin barco guardia","No guard ship"), Pair("Barco guardia · ","Guard ship · "), Pair("País · ","Country · "),
            Pair(" · cancelar encargo (devuelve el oro)"," · cancel order (refunds gold)"), Pair(" · cancelar encargo"," · cancel order"),
            Pair("Clic derecho en el mapa fija la salida de las nuevas unidades.","Right-click the map to set where new units rally."),
            Pair("Ingreso en ","Income in "), Pair("próximo ingreso","next income"),
            Pair("Más opciones · página ","More options · page "), Pair("Ampliar panel","Expand panel"), Pair("Reducir panel","Collapse panel"),
            Pair("Con un edificio seleccionado, su cuadrícula tiene prioridad: Q W E R / A S D F / Z X C V producen y E, A, S, D no dan órdenes de tropa.",
                "With a building selected its grid takes priority: Q W E R / A S D F / Z X C V produce, and E, A, S, D do not issue unit orders."),
            Pair("Con una ciudad o puerto propio seleccionado: produce la unidad de esa casilla de la cuadrícula","With your city or harbor selected: produce the unit in that grid cell"),
            Pair("Si la cuadrícula tiene más de 12 opciones: cambia de página","When the grid has more than 12 options: switch page"),
            Pair("Con tropas: atacar, mover, patrullar, detener, mantener posición","With units: attack, move, patrol, stop, hold position"),
            Pair("Embarcar tropas cercanas · desembarcar la flota","Board nearby troops · unload the fleet"),
            Pair("Seleccionar todo el ejército","Select the whole army"), Pair("Seleccionar la flota","Select the fleet"),
            Pair("Recuperar o guardar un grupo; doble pulsación centra la cámara","Recall or store a group; double-press centers the camera"),
            Pair("Centrar en la selección, la flota o la última alerta","Center on the selection, fleet or latest alert"),
            Pair("Menú · ir a tu base · ir a tu puerto","Menu · go to your base · go to your harbor"),
            Pair("Mantener para ver la clasificación","Hold to show the ranking"), Pair("Escribir en el chat","Type in chat"),
            Pair("Cancelar la orden o deseleccionar","Cancel the order or deselect"), Pair("Mostrar vida y nombres","Show health and names"),
            Pair("Con una tropa ocupada añade la orden: mover, atacar, capturar, seguir, embarcar y desembarcar. Sin Mayús, la orden sustituye la cola. El conmutador de la barra rápida hace lo mismo.",
                "With a busy unit, add the order: move, attack, capture, follow, board and unload. Without Shift the order replaces the queue. The quick-bar switch does the same."),
            Pair("Mantener junto a las flechas: la cámara se mueve más rápido","Hold with the arrows: the camera pans faster"),
            Pair("Encolar","Queue"),
            Pair("Mover la cámara · restablecer la cámara","Pan the camera · reset the camera"),
            Pair("Flechas · Retroceso","Arrows · Backspace"), Pair("Espacio","Space"), Pair("Intro","Enter"),
            // Feedback overlay: log, toasts, alerts, chat and audio settings (BattleHud.Feedback).
            Pair("¡País completado: ","Country completed: "), Pair("Has perdido el país ","You lost the country "), Pair("Has perdido ","You lost "),
            Pair("¡Te atacan en ","Under attack at "), Pair("¡Te atacan!","Under attack!"), Pair(" de oro"," gold"),
            Pair("Escribir un mensaje (Intro)","Write a message (Enter)"), Pair("Escribe un mensaje…","Type a message…"),
            Pair("Enviar","Send"), Pair("Cancelar","Cancel"),
            Pair("SONIDO: SILENCIADO","SOUND: MUTED"), Pair("SONIDO: ACTIVO","SOUND: ON"), Pair("VOLUMEN","VOLUME"), Pair("EFECTOS","EFFECTS"),
            Pair("TEMBLOR DE CÁMARA: ACTIVO","CAMERA SHAKE: ON"), Pair("TEMBLOR DE CÁMARA: INACTIVO","CAMERA SHAKE: OFF"),
            Pair("Volumen general ","Master volume "), Pair(" · efectos "," · effects "), Pair(" · música "," · music "), Pair(" · F8 música"," · F8 music"),
            Pair("MÚSICA: ACTIVA","MUSIC: ON"), Pair("MÚSICA: DESACTIVADA","MUSIC: OFF"), Pair("MÚSICA −","MUSIC −"), Pair("MÚSICA +","MUSIC +"), Pair("Música activada","Music on"), Pair("Música desactivada","Music off"),
            Pair("RIESGUS · Traza tu conquista. Reúne tus ejércitos. Defiende cada frontera.","RIESGUS · Plot your conquest. Rally your armies. Defend every frontier."),
            Pair("Conquista territorial en tiempo real.","Real-time territorial conquest."),
            Pair("Cuatro Riberas","Four Riverlands"), Pair("Las Marcas","The Marches"),
            Pair("Los refuerzos esperan en la hoguera hasta fijar una salida.","Reinforcements wait at the camp until you set a rally point."),
            Pair("Salida fijada. Clic derecho cambia el punto de reunión.","Rally set. Right-click to change it."),
            Pair("Clic derecho en terreno fija la salida de los refuerzos del país.","Right-click terrain to set the country's reinforcement rally."),
            Pair("Cada ciudad de un país completo aporta oro. Las ciudades de países incompletos no añaden ingresos.","Each city in a completed country provides gold. Cities in incomplete countries provide no income."),
            Pair("1 · Selecciona una ciudad. Crea unidades e invade.","1 · Select a city. Recruit units and invade."),
            Pair("2 · La guarnición no sale sin un relevo aliado.","2 · The garrison cannot leave without an allied replacement."),
            Pair("3 · Completa países para obtener oro y refuerzos.","3 · Complete countries to gain gold and reinforcements."),
            Pair("+4 oro base por ronda si conservas una ciudad · País completo: +1 por ciudad y refuerzos · Conquista el 60 %.","+4 base gold each round while you hold a city · Complete country: +1 per city and reinforcements · Conquer 60%."),
            Pair("Las bajas enemigas conceden aparte ¼ de su valor de recompensa; las fracciones se acumulan hasta completar una moneda. No forman parte del ingreso por ronda.","Enemy losses also grant ¼ of their reward value; fractions accumulate into a full coin. This is separate from round income."),
            Pair("falta oro","not enough gold"), Pair(" oro en "," gold in "),
            Pair("Escribe una semilla numérica válida.","Enter a valid numeric seed."), Pair("No se encontró la escena de batalla.","The battle scene could not be found."),
            Pair("JUGADORES · MÁXIMO","PLAYERS · MAXIMUM"), Pair("Queda neutral ","Now neutral: "), Pair(" completa "," completed "),
            Pair("El puerto no tiene una salida marítima segura.","This harbor has no safe sea route."),
            Pair("La tropa no puede llegar al embarque marcado.","The unit cannot reach the marked boarding point."),
            Pair("Selecciona una tropa móvil aliada.","Select an allied mobile unit."),
            Pair("El puerto no tiene una playa o pasarela al alcance del transporte.","No beach or pier is within transport range of this harbor."),
            Pair("Elige una playa o muelle de desembarco marcado.","Choose a marked beach or landing pier."),
            Pair("El transporte navega al desembarco marcado.","The transport is sailing to the marked landing."),
            Pair("No hay una ruta marítima hasta ese destino.","There is no sea route to that destination."),
            Pair("El casco está bloqueado; se cancela el movimiento.","The hull is blocked; the move is cancelled."),
            Pair("Elige un puerto de desembarco.","Choose a landing harbor."), Pair("No hay una ruta marítima segura hasta esa playa.","There is no safe sea route to that beach."),
            Pair("No se puede embarcar con la partida detenida.","Units cannot board while the match is paused."),
            Pair("Esa unidad ya no está dentro del transporte.","That unit is no longer aboard the transport."),
            Pair("El barco guardia necesita un relevo aliado en el puerto.","The guard ship needs an allied replacement at the harbor."),
            Pair("La cola de órdenes está llena.","The order queue is full."), Pair("El defensor necesita un relevo aliado dentro del círculo.","The defender needs an allied replacement inside the circle."),
            Pair("La orden ya no es válida para esa unidad o su objetivo.","That order is no longer valid for the unit or target."),
            Pair("La unidad, el relevo o el objetivo cambió antes de aplicar la orden.","The unit, replacement, or target changed before the order was applied."),
            Pair("No se ha podido aplicar la orden.","The order could not be applied."), Pair("Tipo de barco inválido.","Invalid ship type."),
            Pair("Bando inválido.","Invalid player."), Pair("Reanuda la partida para comprar barcos.","Resume the match to buy ships."),
            Pair("Este puerto no pertenece a tu bando.","This harbor does not belong to you."), Pair("La cola naval está llena.","The naval queue is full."),
            Pair("Oro insuficiente para comprar este barco.","Not enough gold to buy this ship."), Pair("Este muelle sólo entrena Marines.","This dock only trains Marines."),
            Pair("Reanuda la partida para reclutar.","Resume the match to recruit."), Pair("La cola de Marines está llena.","The Marine queue is full."),
            Pair("Límite de soldados alcanzado.","Soldier limit reached."), Pair("Este encargo ya no está en la cola.","That order is no longer queued."),
            Pair("Reanuda la partida para cancelar encargos.","Resume the match to cancel orders."), Pair("Este puerto no tiene ciudad.","This harbor has no city."),
            Pair("Reanuda la partida para construir.","Resume the match to build."), Pair("Este puerto ya tiene una torre.","This harbor already has a tower."),
            Pair("Ya hay una obra en marcha en este puerto.","Construction is already underway at this harbor."),
            Pair("Este puerto sólo recluta Marines.","This harbor only recruits Marines."), Pair("Esta ciudad sólo recluta tropas regulares.","This city only recruits regular troops."),
            Pair("Mejora la ciudad a nivel II para reclutar esta unidad.","Upgrade the city to level II to recruit this unit."),
            Pair("La cola está llena. Pulsa un encargo para cancelarlo.","The queue is full. Select an order to cancel it."),
            Pair("Límite de 100 soldados móviles alcanzado.","Mobile soldier limit of 100 reached."),
            Pair("Oro insuficiente. Recibirás ingresos al terminar la ronda.","Not enough gold. You receive income at the end of the round."),
            Pair("Reanuda la partida para dar esta orden.","Resume the match to issue this order."), Pair("Selecciona una ciudad de tu bando.","Select one of your cities."),
            Pair("Esta ciudad ya tiene una torre.","This city already has a tower."), Pair("Las ciudades conservan su nivel: compra las unidades directamente.","Cities retain their level; recruit units directly."),
            Pair("Ya hay una obra en marcha en esta ciudad.","Construction is already underway in this city."),
            Pair("Cada compra encarga una unidad por edificio compatible. La cantidad y el oro muestran el máximo disponible ahora; las colas no disponibles se omiten.","Each purchase queues one unit at every compatible selected building."),
            Pair("El total incluye soldados, defensores de ciudades y barcos. Los defensores y barcos no consumen plazas de reclutamiento.","Total includes soldiers, city defenders, and ships. Defenders and ships do not use recruitment slots."),
            Pair("Conquista el 60 % de las ciudades. Completa países para recibir refuerzos de sus hogueras.","Conquer 60% of all cities. Complete countries to receive camp reinforcements."),
            Pair("Selección: clic o toque para seleccionar; arrastra un área para seleccionar tropas y edificios.","Selection: click or tap; drag an area to select units and buildings."),
            Pair("Órdenes: clic derecho en PC o una acción seguida de toque en tabletas. B/D embarca y desembarca.","Orders: right-click on PC, or choose an action then tap. B/U boards and unloads."),
            Pair("Cámara: rueda para zoom, arrastre derecho para mover y botón central para girar. En pantalla táctil, dos dedos mueven y amplían; tres dedos giran.","Camera: wheel to zoom, right-drag to pan, middle-drag to rotate. On touch, use two fingers to pan and zoom, three to rotate."),
            Pair("4 de oro y un defensor por puesto. Conquista el 60 % de las ciudades.","Start with 4 gold and one defender per post. Conquer 60% of all cities."),
            Pair("Cargando terreno, ciudades y rutas…","Loading terrain, cities, and routes…"),
            Pair("Añadir cordilleras suaves a Europe y New World","Add gentle mountain ranges to Europe and New World"),
            Pair("Respeta coordenadas y despeja anclajes.","Preserves source coordinates and clears anchors."),
            Pair("Disponible en escenarios importados.","Available on imported scenarios."),
            Pair("Territorio importado a escala con puertos y fronteras reales.","Source-scale territory with ports and borders."),
            Pair("Costa, mesetas y un sur seco para campañas rápidas.","Coast, plateaus, and a dry south for fast campaigns."),
            Pair("Río central, puente y un archipiélago al norte.","Central river, bridge, and northern archipelago."),
            Pair("Europa y América para una conquista de gran escala.","Europe and America for large-scale conquest."),
            Pair("MARCA DEL ALBA","DAWN MARCH"), Pair("VALLE DE LOS PINOS","PINE VALLEY"), Pair("ESCARPA DE PONIENTE","WESTERN ESCARPMENT"),
            Pair("CUENCA DEL FRESNO","ASH BASIN"), Pair("PUERTAS DE ORIENTE","EASTERN GATES"), Pair("SIERRA CARMESÍ","CRIMSON RANGE"),
            Pair("DEHESA DE PONIENTE","WESTERN PASTURE"), Pair("CAMPOS DEL SECANO","DRY FIELDS"), Pair("LOMAS DE AZAFRÁN","SAFFRON HILLS"),
            Pair("COSTA DE SAL","SALT COAST"), Pair("ISLAS DEL NORTE","NORTHERN ISLES"), Pair("MARCA OCCIDENTAL","WESTERN MARCH"),
            Pair("BOSQUES DE PONIENTE","WESTERN FORESTS"), Pair("CUENCA DEL RÍO","RIVER BASIN"), Pair("COSTA OCCIDENTAL","WESTERN COAST"),
            Pair("COLINAS OCCIDENTALES","WESTERN HILLS"), Pair("RIBERA ALTA","HIGH RIVERBANK"), Pair("ALTOS CENTRALES","CENTRAL HIGHLANDS"),
            Pair("FRONTERA ORIENTAL","EASTERN FRONTIER"), Pair("LLANURAS DE LEVANTE","EASTERN PLAINS"), Pair("PUERTAS DEL ESTUARIO","ESTUARY GATES"),
            Pair("ARCHIPIÉLAGO NORTE","NORTHERN ARCHIPELAGO"),
            Pair("Marca del Alba","Dawn March"), Pair("Valle de los Pinos","Pine Valley"), Pair("Escarpa de Poniente","Western Escarpment"),
            Pair("Cuenca del Fresno","Ash Basin"), Pair("Puertas de Oriente","Eastern Gates"), Pair("Sierra Carmesí","Crimson Range"),
            Pair("Dehesa de Poniente","Western Pasture"), Pair("Campos del Secano","Dry Fields"), Pair("Lomas de Azafrán","Saffron Hills"),
            Pair("Costa de Sal","Salt Coast"), Pair("Islas del Norte","Northern Isles"), Pair("Marca Occidental","Western March"),
            Pair("Bosques de Poniente","Western Forests"), Pair("Cuenca del Río","River Basin"), Pair("Costa Occidental","Western Coast"),
            Pair("Colinas Occidentales","Western Hills"), Pair("Ribera Alta","High Riverbank"), Pair("Altos Centrales","Central Highlands"),
            Pair("Frontera Oriental","Eastern Frontier"), Pair("Llanuras de Levante","Eastern Plains"), Pair("Puertas del Estuario","Estuary Gates"),
            Pair("Archipiélago Norte","Northern Archipelago"), Pair("Bahía de Poniente","Western Bay"), Pair("Estrecho del Norte","Northern Strait"),
            Pair("Bahía del Noroeste","Northwest Bay"), Pair("Llano Central","Central Plain"), Pair("Meseta de los Pinos","Pine Plateau"),
            Pair("Bastión del Alba","Dawn Bastion"), Pair("Pinar Alto","High Pinewood"), Pair("Cordillera del Alba","Dawn Range"),
            Pair("Molino Viejo","Old Mill"), Pair("Valdeluz","Brightvale"), Pair("Encinar Central","Central Oakwood"),
            Pair("Cresta de Poniente","Western Ridge"), Pair("Dehesa Norte","Northern Pasture"), Pair("Puerta de Piedra","Stone Gate"),
            Pair("Valle del Fresno","Ash Valley"), Pair("Piedra Vieja","Old Stone"), Pair("Marca del Sur","Southern March"),
            Pair("Torre del Roble","Oak Tower"), Pair("Vigía del Este","Eastern Watch"), Pair("Senda Oriental","Eastern Path"),
            Pair("Torre del Norte","Northern Tower"), Pair("Fortaleza Carmesí","Crimson Fortress"), Pair("Altos de Ceniza","Ash Highlands"),
            Pair("Guardia Oriental","Eastern Guard"), Pair("Encinar Bajo","Lower Oakwood"), Pair("Trigal Dorado","Golden Wheatfields"),
            Pair("Azafrán del Norte","Northern Saffron"), Pair("Olivar de la Marca","March Olive Grove"), Pair("Loma del Sur","Southern Hill"),
            Pair("Costa del Sur","Southern Coast"), Pair("Vigía de la Sal","Salt Watch"), Pair("Isla de la Bruma","Mist Isle"),
            Pair("Dehesa de la Frontera","Frontier Pasture"), Pair("Isla del Viento","Wind Isle"), Pair("Torre de la Sal","Salt Tower"),
            Pair("Bastión Occidental","Western Bastion"), Pair("Bosque Bajo","Lower Forest"), Pair("Paso de Poniente","Western Pass"),
            Pair("Pinar Occidental","Western Pinewood"), Pair("Loma del Roble","Oak Hill"), Pair("Marjal Occidental","Western Marsh"),
            Pair("Cresta del Bosque","Forest Ridge"), Pair("Puerta del Río","River Gate"), Pair("Vega del Sur","Southern Meadow"),
            Pair("Molino del Río","River Mill"), Pair("Vado Bajo","Lower Ford"), Pair("Ribera del Río","Riverbank"),
            Pair("Linde Occidental","Western Border"), Pair("Peña del Mar","Sea Rock"), Pair("Cresta Occidental","Western Ridge"),
            Pair("Puerto Alto","High Harbor"), Pair("Colina del Vado","Ford Hill"), Pair("Senda del Norte","Northern Path"),
            Pair("Paso de los Sauces","Willow Pass"), Pair("Estuario Verde","Green Estuary"), Pair("Bastión Central","Central Bastion"),
            Pair("Altos del Sur","Southern Highlands"), Pair("Loma Central","Central Hill"), Pair("Cerro de Piedra","Stone Hill"),
            Pair("Puerta Oriental","Eastern Gate"), Pair("Cantera Oriental","Eastern Quarry"), Pair("Paso de Levante","Eastern Pass"),
            Pair("Mirador del Río","River Lookout"), Pair("Fuerte del Este","Eastern Fort"), Pair("Paso Central","Central Pass"),
            Pair("Vigía Oriental","Eastern Watch"), Pair("Atalaya del Llano","Plains Watch"), Pair("Costa de Levante","Eastern Coast"),
            Pair("Cresta Oriental","Eastern Ridge"), Pair("Altos del Estuario","Estuary Highlands"), Pair("Puerta del Estuario","Estuary Gate"),
            Pair("Isla del Roble","Oak Isle"), Pair("Vega del Confín","Frontier Meadow"), Pair("Isla del Alba","Dawn Isle"), Pair("Vigía de Levante","Eastern Watch"),
            Pair("LA CONQUISTA EMPIEZA EN","CONQUEST STARTS IN"), Pair("VISTA ESTRATÉGICA","STRATEGIC VIEW"),
            Pair("acerca el mapa para combatir","zoom in to fight"), Pair("rombos: hogueras","diamonds: camps"), Pair("◇ hogueras","◇ camps"),
            Pair("Ha sido eliminado","Has been eliminated"), Pair("ha sido eliminado","has been eliminated"),
            Pair("Has sido eliminado. La partida continúa.","You have been eliminated. The match continues."),
            Pair("conquistado por","captured by"), Pair("Has conquistado","You captured"), Pair("ha conquistado","captured"),
            Pair("País completado:","Country completed:"), Pair("Refuerzos activos.","Reinforcements active."),
            Pair("ORO","GOLD"), Pair("ciudades","cities"), Pair("ciudad","city"), Pair("grupos","groups"), Pair("puertos","ports"), Pair("puerto","port"), Pair("jugadores","players"), Pair("jugador","player"),
            Pair("unidades","units"), Pair("tropas","troops"), Pair("defensores","defenders"), Pair("barcos","ships"), Pair("barco","ship"),
            Pair("oro","gold"), Pair("vida","health"), Pair("alcance","range"), Pair("armadura","armor"), Pair("reclutadas","recruited"),
            Pair("RONDA","ROUND"), Pair("Ronda","Round"), Pair("Semilla","Seed"), Pair("máx.","max."), Pair("tú y","you and"), Pair("Los ","The "),
            Pair("IA","AI"), Pair("ELIMINADO","ELIMINATED"), Pair("UNIDADES SELECCIONADAS","UNITS SELECTED"), Pair("TROPAS SELECCIONADAS","TROOPS SELECTED"),
            Pair("EDIFICIOS SELECCIONADOS","BUILDINGS SELECTED"), Pair("Ciudad retirada","City removed"), Pair("Puerto retirado","Harbor removed"),
            Pair("A BORDO","ABOARD"), Pair("pulsa para desembarcar","click to unload"), Pair("desembarcar esta unidad","unload this unit"),
            Pair("Disponible:","Available:"), Pair("Próxima ronda:","Next round:"), Pair("Ingreso básico:","Base income:"), Pair("Total:","Total:"),
            Pair("Reclutamiento:","Recruitment:"), Pair("plazas","slots"), Pair("incluidos los encargos pendientes","including pending orders"),
            Pair("Ocultar mapa","Hide map"), Pair("MOSTRAR MAPA","SHOW MAP"), Pair("OCULTAR MAPA","HIDE MAP"),
            Pair("PANEO EN BORDES: ACTIVO","EDGE PAN: ON"), Pair("PANEO EN BORDES: INACTIVO","EDGE PAN: OFF"),
            Pair("VELOCIDAD CÁMARA","CAMERA SPEED"), Pair("Rendimiento:","Performance:"), Pair("Recogiendo muestra de rendimiento…","Collecting performance sample…"),
            Pair(" ms medio"," ms average"), Pair(" ms máximo"," ms maximum"), Pair("Controla todo el país","Control the whole country"),
            Pair("HOGUERA","CAMP"), Pair("Punto de reunión","Rally point"), Pair("salida","rally"), Pair("refuerzos","reinforcements"),
            Pair("La partida está detenida.","The match is paused."), Pair("Reanuda la partida","Resume the match"),
            Pair("No hay","There is no"), Pair("Selecciona","Select"), Pair("Límite","Limit"), Pair("Oro insuficiente","Not enough gold"),
            Pair("conservan su nivel","retain their level"), Pair("completada","completed"), Pair("completo","complete"), Pair("incompletos","incomplete"),
            Pair("del Norte","of the North"), Pair("del Sur","of the South"), Pair("del Este","of the East"), Pair("Occidental","Western"), Pair("Oriental","Eastern"),
            Pair("Norte","North"), Pair("Sur","South"), Pair("Este","East"), Pair("Poniente","West"), Pair("Levante","East"),
            Pair("Bastión","Bastion"), Pair("Marca","March"), Pair("Valle","Valley"), Pair("Pinar","Pinewood"), Pair("Bosque","Forest"),
            Pair("Torre","Tower"), Pair("Puerta","Gate"), Pair("Costa","Coast"), Pair("Isla","Isle"), Pair("Lomas","Hills"), Pair("Colinas","Hills"),
            Pair("Sierra","Range"), Pair("Cuenca","Basin"), Pair("Ribera","Riverbank"), Pair("Archipiélago","Archipelago"), Pair("Frontera","Frontier"),
            Pair("Carmesí","Crimson"), Pair("Piedra","Stone"), Pair("Roble","Oak"), Pair("Río","River"), Pair("Alba","Dawn"), Pair("Viento","Wind"),
            Pair("Vigía","Watch"), Pair("Cresta","Ridge"), Pair("Loma","Hill"), Pair("Altos","Highlands"), Pair("Campos","Fields")
        };

        // Unit names and roles come only from units.json (UnitCatalog); they join the exact
        // table when the catalog is bound, with the same longest-first substring order.
        static KeyValuePair<string,string>[] exactPhrases;
        static Dictionary<string,string> unitText;
        static int unitTextRevision=-1;
        static KeyValuePair<string,string>[] ExactPhrases{get{RefreshUnitText();return exactPhrases;}}

        static KeyValuePair<string,string> Pair(string source,string english) => new KeyValuePair<string,string>(source,english);

        static void RefreshUnitText()
        {
            int revision=RiskAI.Core.UnitCatalog.IsBound?RiskAI.Core.UnitCatalog.Revision:-1;
            if(exactPhrases!=null&&unitTextRevision==revision)return;
            unitText=new Dictionary<string,string>();
            if(revision>=0)
                for(int i=0;i<RiskAI.Core.UnitCatalog.Count;i++)
                {
                    ref readonly var type=ref RiskAI.Core.UnitCatalog.At(i);
                    if(type.Domain==RiskAI.Core.UnitDomain.Static)continue;
                    AddUnitText(type.Name,type.NameEn);AddUnitText(type.Role,type.RoleEn);
                }
            var entries=new List<KeyValuePair<string,string>>(Exact.Count+unitText.Count);
            foreach(var entry in Exact)entries.Add(entry);
            foreach(var entry in unitText)entries.Add(entry);
            entries.Sort((a,b)=>b.Key.Length.CompareTo(a.Key.Length));
            exactPhrases=entries.ToArray();unitTextRevision=revision;
        }
        static void AddUnitText(string spanish,string english)
        {
            if(string.IsNullOrEmpty(spanish)||string.IsNullOrEmpty(english)||spanish==english||Exact.ContainsKey(spanish))return;
            unitText[spanish]=english;
        }

        public static string Localize(string source) => IsSpanish ? source : EnglishOf(source);

        /// <summary>English form of a Spanish source string, whatever the current language (chat aliases).</summary>
        public static string EnglishOf(string source)
        {
            if(string.IsNullOrEmpty(source))return source;
            if(Exact.TryGetValue(source,out string exact))return exact;
            RefreshUnitText();
            if(unitText.TryGetValue(source,out exact))return exact;
            string result=source;
            for(int i=0;i<Phrases.Length;i++)if(result.IndexOf(Phrases[i].Key,StringComparison.Ordinal)>=0)result=result.Replace(Phrases[i].Key,Phrases[i].Value);
            for(int i=0;i<ExactPhrases.Length;i++)if(result.IndexOf(ExactPhrases[i].Key,StringComparison.Ordinal)>=0)result=result.Replace(ExactPhrases[i].Key,ExactPhrases[i].Value);
            return result;
        }
    }
}
