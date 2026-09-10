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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => Language = GameLanguage.English;

        public static void Toggle() => Language = IsSpanish ? GameLanguage.English : GameLanguage.Spanish;
        public static void Set(GameLanguage language) => Language = language;

        static readonly Dictionary<string,string> Exact = new Dictionary<string,string>
        {
            ["DOMINIOS"]="DOMINIONS", ["CONQUISTA"]="CONQUEST", ["PAUSADO"]="PAUSED",
            ["PAUSA"]="PAUSE", ["Pausa"]="Pause", ["CONTINUAR"]="RESUME", ["Continuar"]="Resume",
            ["VOLVER"]="BACK", ["Menú"]="Menu", ["Mapa"]="Map", ["Ranking"]="Ranking",
            ["Partida"]="Match", ["Controles"]="Controls", ["VICTORIA"]="VICTORY", ["DERROTA"]="DEFEAT",
            ["COMENZAR LA CONQUISTA"]="START CONQUEST", ["PREPARANDO LA CONQUISTA"]="PREPARING CONQUEST",
            ["ELEGIDO"]="SELECTED", ["NUEVA SEMILLA"]="NEW SEED", ["SEMILLA"]="SEED",
            ["REPARTO INICIAL"]="STARTING LAYOUT", ["DIFICULTAD DE IA"]="AI DIFFICULTY",
            ["RELIEVE IMPORTADO"]="IMPORTED RELIEF", ["NUEVA PARTIDA"]="NEW MATCH",
            ["NUEVA PARTIDA · ELEGIR MAPA"]="NEW MATCH · CHOOSE MAP", ["CENTRAR MAPA"]="CENTER MAP",
            ["RESTABLECER CÁMARA"]="RESET CAMERA", ["OCULTAR MAPA TÁCTICO"]="HIDE TACTICAL MAP",
            ["MOSTRAR MAPA TÁCTICO"]="SHOW TACTICAL MAP", ["DESGLOSE DEL ORO"]="GOLD BREAKDOWN",
            ["UNIDADES"]="UNITS", ["CLASIFICACIÓN · CIUDADES"]="RANKING · CITIES",
            ["ÓRDENES DE HOGUERA"]="CAMP ORDERS", ["BORRAR SALIDA"]="CLEAR RALLY",
            ["Mover"]="Move", ["Atacar"]="Attack", ["Patrullar"]="Patrol", ["Detener"]="Stop",
            ["Mantener"]="Hold", ["Centrar"]="Focus", ["Embarcar"]="Board", ["Desembarcar"]="Unload",
            ["Puerto"]="Harbor", ["Espadachín"]="Swordsman", ["Ballestero"]="Crossbowman",
            ["Caballero"]="Knight", ["Mago"]="Mage", ["Mortero"]="Mortar", ["Sanador"]="Healer",
            ["Primera línea"]="Front line", ["Ataque a distancia"]="Ranged attack", ["Caballería pesada"]="Heavy cavalry",
            ["Daño de área"]="Area damage", ["Área a larga distancia"]="Long-range area damage",
            ["Sana aliados · 25 vida"]="Heals allies · 25 health", ["Pistolero de puerto"]="Harbor pistolier",
            ["Caballería de puerto"]="Harbor cavalry", ["Caballería veterana de puerto"]="Veteran harbor cavalry",
            ["Preparado"]="Ready", ["Moviendo"]="Moving", ["En combate"]="In combat", ["Patrullando"]="Patrolling",
            ["Siguiendo"]="Following", ["Manteniendo posición"]="Holding position", ["En puerto"]="In harbor",
            ["Navegando"]="Sailing", ["Neutral"]="Neutral", ["Tú"]="You",
            ["Rojo"]="Red", ["Azul"]="Blue", ["Turquesa"]="Teal", ["Violeta"]="Purple",
            ["Amarillo"]="Yellow", ["Naranja"]="Orange", ["Verde"]="Green", ["Rosa"]="Pink",
            ["Gris"]="Gray", ["Azul claro"]="Light Blue", ["Verde oscuro"]="Dark Green",
            ["Marrón"]="Brown", ["Granate"]="Maroon", ["Azul marino"]="Navy", ["Cian"]="Cyan", ["Magenta"]="Magenta",
            ["Las Marcas"]="The Marches", ["Cuatro Riberas"]="Four Riverlands",
            ["LAS MARCAS"]="THE MARCHES", ["CUATRO RIBERAS"]="FOUR RIVERLANDS",
            ["NEW WORLD · EUROPA Y AMÉRICA"]="NEW WORLD · EUROPE AND AMERICA",
            ["Elige tu campo de batalla"]="Choose a battlefield", ["Prepara la expedición"]="Prepare the expedition",
            ["Ciudades al azar"]="Random cities", ["Países iniciales"]="Starting countries", ["Posiciones fijas"]="Fixed positions",
            ["Relajada · tácticas sencillas"]="Relaxed · simple tactics", ["Estándar · mayor coordinación"]="Standard · coordinated tactics"
        };

        static readonly KeyValuePair<string,string>[] Phrases = {
            Pair("RISKAI  ·  Traza tu conquista. Reúne tus ejércitos. Defiende cada frontera.","Real-time territorial conquest."),
            Pair("Conquista territorial en tiempo real.","Real-time territorial conquest."),
            Pair("Cuatro Riberas","Four Riverlands"), Pair("Las Marcas","The Marches"),
            Pair("Los refuerzos esperan en la hoguera hasta fijar una salida.","Reinforcements wait at the camp until you set a rally point."),
            Pair("Salida fijada. Clic derecho cambia el punto de reunión.","Rally set. Right-click to change it."),
            Pair("Clic derecho en terreno fija la salida de los refuerzos del país.","Right-click terrain to set the country's reinforcement rally."),
            Pair("Cada ciudad de un país completo aporta oro. Las ciudades de países incompletos no añaden ingresos.","Each city in a completed country provides gold. Cities in incomplete countries provide no income."),
            Pair("Las bajas enemigas conceden aparte ¼ de su valor de recompensa; las fracciones se acumulan hasta completar una moneda. No forman parte del ingreso por ronda.","Enemy losses also grant ¼ of their reward value; fractions accumulate into a full coin. This is separate from round income."),
            Pair("falta oro","not enough gold"), Pair(" oro en "," gold in "),
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
            Pair("Archipiélago Norte","Northern Archipelago"),
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

        static readonly KeyValuePair<string,string>[] ExactPhrases=BuildExactPhrases();

        static KeyValuePair<string,string> Pair(string source,string english) => new KeyValuePair<string,string>(source,english);

        static KeyValuePair<string,string>[] BuildExactPhrases()
        {
            var entries=new KeyValuePair<string,string>[Exact.Count];int index=0;
            foreach(var entry in Exact)entries[index++]=entry;
            Array.Sort(entries,(a,b)=>b.Key.Length.CompareTo(a.Key.Length));
            return entries;
        }

        public static string Localize(string source)
        {
            if(IsSpanish||string.IsNullOrEmpty(source))return source;
            if(Exact.TryGetValue(source,out string exact))return exact;
            string result=source;
            for(int i=0;i<Phrases.Length;i++)if(result.IndexOf(Phrases[i].Key,StringComparison.Ordinal)>=0)result=result.Replace(Phrases[i].Key,Phrases[i].Value);
            for(int i=0;i<ExactPhrases.Length;i++)if(result.IndexOf(ExactPhrases[i].Key,StringComparison.Ordinal)>=0)result=result.Replace(ExactPhrases[i].Key,ExactPhrases[i].Value);
            return result;
        }
    }
}
