using System.Collections.Generic;
using System.Linq;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiskAI
{
    public sealed class BattleHud : MonoBehaviour
    {
        public static float Scale => Mathf.Min(1, Screen.width / 1600f);
        public static float BottomPixels => 208 * Scale;
        public static float TopPixels => 48 * Scale;
        BattleSession session;
        RtsController controller;
        Camera cam;
        readonly Dictionary<UnitKind, Texture2D> portraits = new Dictionary<UnitKind, Texture2D>();
        string tooltip;
        string seedText;
        Texture2D minimapTexture;
        float width, height, bottom;
        Vector2 MousePoint => new Vector2(controller.Pointer.x / Scale, (Screen.height - controller.Pointer.y) / Scale);
        public void Initialize(BattleSession battle, RtsController input, Camera camera)
        {
            session = battle; controller = input; cam = camera;
            seedText=session.Seed.ToString();
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind))) portraits[kind] = Resources.Load<Texture2D>("Portraits/" + BattleRules.Model(kind));
        }
        static void Text(Rect r, string text, GUIStyle style = null) => GUI.Label(r, text, style ?? RtsSkin.Text);
        static void Label(float x, float y, float w, string text, GUIStyle style = null) => Text(new Rect(x, y, w, 26), text, style);
        bool Button(Rect rect, string text, string hint = null)
        {
            if (hint != null && rect.Contains(MousePoint)) tooltip = hint;
            return GUI.Button(rect, text, RtsSkin.Button);
        }
        void OnGUI()
        {
            if (!session || !controller) return;
            RtsSkin.Initialize(); width = Screen.width / Scale; height = Screen.height / Scale; bottom = height - 208; tooltip = null;
            Matrix4x4 previous = GUI.matrix; GUI.matrix = Matrix4x4.Scale(Vector3.one * Scale);
            try { Draw(); } finally { GUI.matrix = previous; }
        }
        void Draw()
        {
            DrawWorld();
            RtsSkin.Frame(new Rect(0, 0, width, 48));
            Label(22, 7, 270, "RISKAI · LAS MARCAS", RtsSkin.Title);
            Label(23, 30, 235, "V0.9 · F3 PUERTO · N FLOTA", RtsSkin.Tiny);
            Label(270, 13, 180, session.Economy.Gold[0] + " ORO  +" + session.Economy.Income(0) + "/RONDA", RtsSkin.Small);
            Label(455, 13, 150, session.Population(0) + " TROPAS / IA " + session.Population(1), RtsSkin.Small);
            Label(610, 13, 155, "RONDA " + session.Economy.Round + " · " + Mathf.CeilToInt(60 - session.Economy.ElapsedInRound) + " s", RtsSkin.Small);
            Label(770, 13, 245, session.Towns.Count(t => t.State.Owner == 0) + " / " + MapLayout.Towns.Length + " CIUDADES", RtsSkin.Small);
            Label(1030,13,330,"IA "+session.DifficultyName+(session.BattleTime<session.AiFirstOffensiveTime?" / prepara tus defensas":""),RtsSkin.Small);
            if (Button(new Rect(width - 195, 13, 86, 35), session.Paused ? "Continuar" : "Pausa", "F10 · pausar o continuar")) session.TogglePause();
            if (Button(new Rect(width - 101, 13, 81, 35), "Ayuda", "F1 · controles y reglas")) controller.HelpVisible = !controller.HelpVisible;
            RtsSkin.Frame(new Rect(0, bottom, width, 208));
            DrawMinimap(new Rect(16, bottom + 23, 208, 156));
            Label(20, bottom + 181, 205, "CLIC: CÁMARA · DER.: ORDEN", RtsSkin.Tiny);
            float actions = width - 660;
            if (controller.SelectedHarbor) HarborDetails(controller.SelectedHarbor,actions);
            else if(controller.Fleet.Count>0)FleetDetails(actions);
            else if (controller.SelectedTown) { TownDetails(controller.SelectedTown); Shop(controller.SelectedTown, actions); }
            else if (controller.Selection.Count > 0) { ArmyDetails(actions); Orders(actions); }
            else if (controller.InspectedTarget) { TargetDetails(controller.InspectedTarget); Orders(actions); }
            else
            {
                Label(255, bottom + 23, 650, "LAS MARCAS ESPERAN TU ESTANDARTE", RtsSkin.Title);
                Label(255, bottom + 62, 650, "Selecciona tus soldados con una caja o pulsa E para elegir tu ejército.");
                Label(255, bottom + 91, 650, "Selecciona una ciudad azul para comprar tropas, construir y mejorar.", RtsSkin.Small);
                Label(255, bottom + 121, 650, "A + clic avanza combatiendo · clic derecho mueve o ataca al enemigo.", RtsSkin.Small);
                Orders(actions);
            }
            for (int i = 0; i < Mathf.Min(3, session.Messages.Count); i++)
            {
                var r = new Rect(16, bottom - 28 - i * 23, 680, 22); RtsSkin.Fill(r, new Color(.035f, .04f, .03f, .83f));
                Label(r.x + 7, r.y, r.width - 12, session.Messages[i], RtsSkin.Small);
            }
            if (controller.Dragging)
            {
                Rect real = controller.SelectionRect; var r = new Rect(real.x / Scale, real.y / Scale, real.width / Scale, real.height / Scale);
                RtsSkin.Fill(r, new Color(.4f, 1, .4f, .12f)); Outline(r, new Color(.55f, 1, .5f));
            }
            if (controller.OrderCursor) Label(MousePoint.x + 20, MousePoint.y + 15, 300, controller.AttackCursor ? "ATACAR · elige enemigo o terreno" : controller.PatrolCursor ? "PATRULLAR · elige destino" : "MOVER · elige destino", RtsSkin.Small);
            if (controller.Hovered && !controller.OrderCursor)
            {
                var target = controller.Hovered; string name = target is Soldier unit ? BattleRules.Name(unit.Kind) : target is Ship ship?ship.DisplayName:"Torre de guardia";
                Label(MousePoint.x + 19, MousePoint.y + 19, 240, name + " · " + Mathf.CeilToInt(target.Health) + " vida", RtsSkin.Small);
            }
            if (tooltip != null)
            {
                var r = new Rect(width - 575, bottom - 105, 560, 88); RtsSkin.Frame(r, RtsSkin.Gold);
                Text(new Rect(r.x + 15, r.y + 12, r.width - 30, r.height - 20), tooltip, new GUIStyle(RtsSkin.Text) { wordWrap = true });
            }
            if (session.Paused) Text(new Rect(width / 2 - 210, 80, 420, 32), "PAUSA · F10 PARA CONTINUAR", RtsSkin.Center);
            for (int team = 0; team < 2; team++) if (session.VictoryProgress[team] > 0)
                Text(new Rect(width / 2 - 220, 117 + 32 * team, 440, 30), (team == 0 ? "Tu victoria" : "Victoria enemiga") + " en " + Mathf.CeilToInt(BattleRules.VictoryHoldSeconds - session.VictoryProgress[team]) + " s · objetivo " + session.VictoryTarget, RtsSkin.Center);
            if (controller.HelpVisible) Help();
            if (session.Winner >= 0) Result();
        }
        void Resource(float x, string value, string detail) { Label(x, 7, 280, value, RtsSkin.Title); Label(x, 35, 295, detail, RtsSkin.Tiny); }
        void HarborDetails(Harbor harbor,float x)
        {
            Label(253,bottom+18,650,harbor.DisplayName,RtsSkin.Title);
            Label(253,bottom+50,620,harbor.Owner==0?"PUERTO ALIADO":harbor.Owner==1?"PUERTO ENEMIGO":"PUESTO INSULAR NEUTRAL",RtsSkin.Small);
            Label(253,bottom+80,625,harbor.IsIsland?"Desembarca infantería para capturar. +8 oro por ronda.":"El puerto cambia de bando junto a "+harbor.LinkedTown.DisplayName+".",RtsSkin.Small);
            Label(253,bottom+110,610,"Cola naval: "+harbor.QueueCount+" / 3 · pulsa un encargo para cancelarlo",RtsSkin.Tiny);
            for(int i=0;i<harbor.QueueCount;i++)
            {
                var r=new Rect(254+i*125,bottom+142,118,42);if(Button(r,harbor.QueuedKind(i)==ShipKind.Galley?"Galera":"Transporte"))controller.Feedback(harbor.CancelTraining(i));
                if(i==0)RtsSkin.Bar(new Rect(r.x,r.yMax+3,r.width,5),harbor.TrainingProgress,RtsSkin.Gold);
            }
            Label(x,bottom+16,590,"ASTILLERO",RtsSkin.Title);
            bool enabled=GUI.enabled;GUI.enabled=harbor.Owner==0&&!session.Paused;
            if(Button(new Rect(x,bottom+51,220,62),"[Q] Galera · 75 oro","500 vida · asedio 20 · alcance 17 · armadura 2 · 4 s"))controller.BuyShip(ShipKind.Galley);
            if(Button(new Rect(x+231,bottom+51,220,62),"[W] Transporte · 45 oro","300 vida · 6 soldados · sin ataque · armadura 1 · 6 s"))controller.BuyShip(ShipKind.Transport);
            GUI.enabled=enabled;
            if(Button(new Rect(x,bottom+126,220,44),"[N] Seleccionar flota"))controller.SelectFleet();
            if(Button(new Rect(x+231,bottom+126,220,44),"[F3] Mi puerto"))controller.FocusHarbor();
            Label(x,bottom+182,590,"Clic derecho en un muelle: navegar y desembarcar",RtsSkin.Tiny);
        }
        void FleetDetails(float x)
        {
            var ship=controller.Fleet.FirstOrDefault(s=>s&&s.IsAlive);if(!ship)return;
            Label(253,bottom+18,600,controller.Fleet.Count==1?ship.DisplayName:"FLOTA · "+controller.Fleet.Count+" BARCOS",RtsSkin.Title);
            Label(253,bottom+50,620,Mathf.CeilToInt(ship.Health)+" / "+ship.MaxHealth+" VIDA · "+ship.OrderLabel,RtsSkin.Small);
            RtsSkin.Bar(new Rect(254,bottom+83,255,8),ship.Health/ship.MaxHealth,new Color(.4f,.8f,.4f));
            int cargo=controller.Fleet.Sum(s=>s?s.CargoCount:0),capacity=controller.Fleet.Count(s=>s&&s.Kind==ShipKind.Transport)*6;
            Label(253,bottom+105,650,capacity>0?"TROPAS EMBARCADAS · "+cargo+" / "+capacity:"20 DAÑO DE ASEDIO · 17 ALCANCE · 2 ARMADURA",RtsSkin.Small);
            Label(253,bottom+137,630,"B: embarca tropas junto al muelle. D: desembarca cerca del puerto.",RtsSkin.Small);
            Label(253,bottom+171,650,"Clic derecho en un puerto: navegar y desembarcar allí.",RtsSkin.Tiny);
            if(Button(new Rect(x,bottom+28,190,52),"[M] Navegar"))controller.ArmMove();
            if(Button(new Rect(x+200,bottom+28,190,52),"[A] Atacar"))controller.ArmAttack();
            if(Button(new Rect(x+400,bottom+28,190,52),"[S] Detener"))controller.Stop();
            if(Button(new Rect(x,bottom+94,190,52),"[B] Embarcar"))controller.BoardNearby();
            if(Button(new Rect(x+200,bottom+94,190,52),"[D] Desembarcar"))controller.UnloadFleet();
            if(Button(new Rect(x+400,bottom+94,190,52),"[F3] Puerto"))controller.FocusHarbor();
            Label(x,bottom+176,590,"Galera: combate naval y costa · Transporte: conquistar islas",RtsSkin.Tiny);
        }
        void Portrait(Rect r, UnitKind kind, Color border)
        {
            RtsSkin.Frame(r, border);
            if (portraits.TryGetValue(kind, out var image) && image) GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), image, ScaleMode.ScaleToFit);
        }
        void TownDetails(Settlement town)
        {
            string owner = town.State.Owner == 0 ? "ALIANZA DEL ALBA" : town.State.Owner == 1 ? "FRONTERA CARMESÍ" : "CIUDAD NEUTRAL";
            Label(252, bottom + 15, 670, town.DisplayName + "  ·  " + (town.State.Level == 1 ? "NIVEL I" : "NIVEL II"), RtsSkin.Title);
            Label(253, bottom + 45, 640, owner + "   /   +" + town.Income + " ORO POR RONDA", RtsSkin.Small);
            var country = town.State.Country >= 0 && town.State.Country < MapLayout.Countries.Length ? MapLayout.Countries[town.State.Country] : default(MapLayout.Country);
            int countryOwned = town.State.Country < 0 ? 0 : session.Towns.Count(t => t.State.Country == town.State.Country && t.State.Owner == 0);
            int countryIncome=session.Towns.Where(t=>t.State.Country==town.State.Country).Sum(t=>t.PotentialIncome);
            string countryLine = town.State.Country >= 0 ? country.Name + "  ·  " + countryOwned + "/2  ·  " + (session.Economy.CountryOwner(town.State.Country) == 0 ? "+" + countryIncome + " oro/ronda · " + country.PerTurn + " " + BattleRules.Name(country.Reinforcement) + "/60 s (máx " + country.PerTurn * 5 + ")" : "Completa el país para cobrar") : "Completa el país para cobrar";
            Label(253, bottom + 73, 680, countryLine, RtsSkin.Small);
            if (town.Defense.IsAlive)
            {
                Label(253, bottom + 99, 300, "TORRE " + Mathf.CeilToInt(town.Defense.Health) + " / " + town.Defense.MaxHealth, RtsSkin.Tiny);
                RtsSkin.Bar(new Rect(460, bottom + 106, 170, 8), town.Defense.Health / town.Defense.MaxHealth, new Color(.62f,.75f,.44f));
            }
            else Label(253, bottom + 99, 390, "Sin torre · ciudad expuesta", RtsSkin.Tiny);
            Label(253, bottom + 127, 490, town.QueueCount > 0 ? "RECLUTANDO · PULSA UN ENCARGO PARA CANCELAR" : "COLA DE RECLUTAMIENTO VACÍA", RtsSkin.Tiny);
            for (int i = 0; i < 5; i++)
            {
                var rect = new Rect(254 + i * 58, bottom + 151, 50, 46); RtsSkin.Frame(rect);
                if (i >= town.QueueCount) continue;
                UnitKind kind = town.QueuedKind(i); Portrait(rect, kind, i == 0 ? RtsSkin.Gold : new Color(.5f,.5f,.4f));
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) controller.Feedback(town.CancelTraining(i));
                if (rect.Contains(MousePoint)) tooltip = "Cancelar " + BattleRules.Name(kind) + " · devuelve " + BattleRules.Cost(kind) + " de oro.";
                if (i == 0) RtsSkin.Bar(new Rect(rect.x + 2, rect.yMax - 7, rect.width - 4, 5), town.TrainingProgress, RtsSkin.Gold);
            }
            if (town.QueueCount > 0) Label(565, bottom + 164, 330, BattleRules.Name(town.TrainingKind) + " · " + Mathf.CeilToInt((1 - town.TrainingProgress) * BattleRules.TrainTime(town.TrainingKind)) + " s", RtsSkin.Small);
        }
        void Shop(Settlement town, float x)
        {
            Label(x, bottom + 15, 320, "RECLUTAR UNIDADES", RtsSkin.Small);
            for (int i = 0; i < 6; i++) UnitCard(new Rect(x + (i % 3) * 108, bottom + 43 + (i / 3) * 78, 102, 72), (UnitKind)i, town);
            float buildX = x + 335;
            Label(buildX, bottom + 15, 300, "FORTIFICACIONES", RtsSkin.Small);
            string tower = town.Defense.IsAlive ? (town.Defense.Team==town.State.Owner?"TORRE ACTIVA":"TORRE ENEMIGA")+"\nDefiende hasta 8,5 m" : "[T] TORRE DE GUARDIA\n60 oro · 7 s";
            bool enabled = GUI.enabled;
            bool canBuild = town.State.Owner == 0 && !town.Building && !session.Paused && session.Winner < 0;
            GUI.enabled = enabled && canBuild && !town.Defense.IsAlive && session.Economy.Gold[0] >= BattleRules.TowerCost;
            if (Button(new Rect(buildX, bottom + 43, 306, 61), tower, "Torre de guardia\n550 vida · 51–58 daño perforante / 1,5 s · armadura fortificada 3 · alcance 8,5. Los morteros son eficaces contra ella.")) controller.BuildTower();
            bool countryControlled = town.State.Country >= 0 && session.Economy.CountryOwner(town.State.Country) == 0;
            string upgrade = town.State.Level == 2 ? "CIUDAD DE NIVEL II\nGuardias, magos y morteros" : "[U] MEJORAR A NIVEL II\n90 oro · 7 s" + (countryControlled ? " · +6 oro por ronda" : "");
            GUI.enabled = enabled && canBuild && town.State.Level < 2 && session.Economy.Gold[0] >= BattleRules.UpgradeCost;
            if (Button(new Rect(buildX, bottom + 114, 306, 62), upgrade, "Mejora de ciudad\nDesbloquea guardias reales, magos y morteros" + (countryControlled ? "; añade +6 de oro por ronda." : ". Completa el país para cobrar sus ingresos."))) controller.UpgradeTown();
            GUI.enabled = enabled;
            if (town.Building)
            {
                RtsSkin.Bar(new Rect(buildX, bottom + 183, 306, 7), town.ProjectProgress, RtsSkin.Gold);
                Text(new Rect(buildX, bottom + 191, 306, 15), town.ProjectName + " en construcción", RtsSkin.Tiny);
            }
        }
        void UnitCard(Rect r, UnitKind kind, Settlement town)
        {
            bool available = town.State.Owner == 0 && town.State.Level >= BattleRules.RequiredLevel(kind);
            bool affordable = session.Economy.Gold[0] >= BattleRules.Cost(kind);
            bool previous = GUI.enabled; GUI.enabled = previous && available && affordable && !session.Paused && town.QueueCount < 5;
            if (GUI.Button(r, GUIContent.none, RtsSkin.Button)) controller.Recruit(kind);
            GUI.enabled = previous;
            Portrait(new Rect(r.x + 5, r.y + 5, 44, 45), kind, available ? RtsSkin.Gold : Color.gray);
            Label(r.x + 54, r.y + 5, 48, BattleRules.Cost(kind) + " oro", RtsSkin.Small);
            Label(r.x + 54, r.y + 27, 48, available ? "[" + BattleRules.Hotkey(kind) + "]" : "Niv. II", RtsSkin.Tiny);
            Text(new Rect(r.x+3,r.y+51,r.width-6,20),BattleRules.Name(kind),new GUIStyle(RtsSkin.Small){alignment=TextAnchor.MiddleCenter,fontSize=12});
            if (!available) RtsSkin.Fill(new Rect(r.x + 5, r.y + 5, 44, 45), new Color(0,0,0,.5f));
            if (r.Contains(MousePoint)) tooltip = BattleRules.Name(kind) + " · " + BattleRules.Role(kind) + "\n" + BattleRules.Health(kind) + " vida · " + BattleRules.DamageRange(kind) + " daño · alcance " + BattleRules.Range(kind) + (kind == UnitKind.Mage ? "\nSus proyectiles dañan también a enemigos cercanos." : "\n" + (!available ? "Necesita una ciudad tuya de nivel II." : affordable ? "Pulsa para añadir a la cola de reclutamiento." : "Necesitas más oro para comprar esta unidad."));
        }
        void ArmyDetails(float actions)
        {
            Soldier first = controller.Selection[0]; TargetDetails(first);
            Label(635, bottom + 19, actions - 650, controller.Selection.Count + " SELECCIONADOS", RtsSkin.Small);
            for (int i = 0; i < Mathf.Min(24, controller.Selection.Count); i++)
            {
                Soldier unit = controller.Selection[i]; var r = new Rect(635 + (i % 8) * 36, bottom + 49 + (i / 8) * 46, 32, 39);
                Portrait(r, unit.Kind, new Color(.5f,.56f,.38f));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { controller.SelectOnly(unit); return; }
                RtsSkin.Bar(new Rect(r.x + 1, r.yMax - 4, r.width - 2, 4), unit.Health / unit.MaxHealth, new Color(.48f,.85f,.3f));
            }
        }
        void TargetDetails(CombatTarget target)
        {
            if(target is Ship ship)
            {
                Label(253,bottom+23,590,ship.DisplayName+" · FLOTA ENEMIGA",RtsSkin.Title);
                Label(253,bottom+61,590,Mathf.CeilToInt(ship.Health)+" / "+ship.MaxHealth+" vida · armadura pesada "+ship.Armor,RtsSkin.Small);
                Label(253,bottom+95,590,ship.Kind==ShipKind.Galley?"20 daño de asedio cada 1,5 s · alcance 17":"Transporte · seis plazas · sin ataque",RtsSkin.Small);return;
            }
            if(target is DefenseTower tower)
            {
                Label(253,bottom+23,590,"TORRE DE GUARDIA",RtsSkin.Title);
                Label(253,bottom+61,590,Mathf.CeilToInt(tower.Health)+" / 550 vida · fortificada · armadura 3",RtsSkin.Small);
                Label(253,bottom+95,590,"51–58 daño perforante cada 1,5 s · alcance 8,5",RtsSkin.Small);
                Label(253,bottom+129,590,"Vulnerable al asedio. El mortero dispara desde más lejos.",RtsSkin.Small);
                return;
            }
            if (!(target is Soldier unit)) return;
            Portrait(new Rect(252, bottom + 26, 129, 158), unit.Kind, VisualFactory.TeamColor(unit.Team));
            Label(396, bottom + 21, 238, BattleRules.Name(unit.Kind), RtsSkin.Title);
            Label(396, bottom + 52, 236, BattleRules.Role(unit.Kind), RtsSkin.Small);
            Label(396, bottom + 80, 225, Mathf.CeilToInt(unit.Health) + " / " + unit.MaxHealth + " vida", RtsSkin.Small);
            RtsSkin.Bar(new Rect(396, bottom + 107, 205, 9), unit.Health / unit.MaxHealth, new Color(.45f,.81f,.3f));
            Label(396, bottom + 130, 225, "DAÑO " + BattleRules.DamageRange(unit.Kind) + "   ALCANCE " + BattleRules.Range(unit.Kind), RtsSkin.Tiny);
            Label(396, bottom + 153, 230, AttackName(unit.AttackType)+" · "+ArmorName(unit.ArmorType)+" "+unit.Armor,RtsSkin.Tiny);
            Label(396, bottom + 174, 230, unit.Team == 0 ? unit.OrderLabel : "UNIDAD ENEMIGA", RtsSkin.Tiny);
        }
        static string AttackName(AttackKind kind)=>kind==AttackKind.Siege?"Asedio":kind==AttackKind.Magic?"Mágico":kind==AttackKind.Piercing?"Perforante":"Normal";
        static string ArmorName(ArmorKind kind)=>kind==ArmorKind.Fortified?"fortificada":kind==ArmorKind.Heavy?"pesada":kind==ArmorKind.Medium?"media":kind==ArmorKind.Light?"ligera":"sin coraza";
        void Orders(float x)
        {
            Countries(x);
            x = width - 252;
            Label(x, bottom + 11, 236, "ÓRDENES", RtsSkin.Small);
            string[] names = { "Mover", "Detener", "Atacar", "Mantener", "Patrullar", "Ejército", "Centrar", "Vista", "Capital" };
            string[] keys = { "M", "S", "A", "H", "P", "E", "ESP", "←", "F2" };
            string[] tips = { "M · Mover al destino, útil para retirarse.", "S · Detenerse y reaccionar a enemigos cercanos.", "A · Elige un enemigo; o terreno para avanzar combatiendo.", "H · Atacar dentro de alcance sin perseguir.", "P · Recorrer dos puntos combatiendo por el camino.", "E · Seleccionar todo tu ejército.", "Espacio · Centrar la cámara en tu selección.", "Retroceso · Restablecer la cámara.", "F2 · Seleccionar una ciudad tuya y centrar la cámara." };
            for (int i = 0; i < 9; i++)
            {
                var r = new Rect(x + (i % 3) * 79, bottom + 35 + (i / 3) * 55, 73, 50);
                bool clicked = GUI.Button(r, GUIContent.none, RtsSkin.Button);
                GUI.DrawTexture(new Rect(r.x + 22, r.y + 3, 29, 29), RtsGlyphs.Get(i));
                Text(new Rect(r.x + 2, r.y + 30, r.width - 4, 18), names[i], new GUIStyle(RtsSkin.Center) { fontSize = 10 });
                Text(new Rect(r.x + 4, r.y + 2, 28, 15), keys[i], RtsSkin.Tiny);
                if (r.Contains(MousePoint)) tooltip = tips[i];
                if (!clicked) continue;
                switch(i) { case 0:controller.ArmMove();break;case 1:controller.Stop();break;case 2:controller.ArmAttack();break;case 3:controller.Hold();break;case 4:controller.ArmPatrol();break;case 5:controller.SelectAll();break;case 6:controller.FocusSelection();break;case 7:controller.CameraRig.ResetView();break;case 8:controller.FocusHome();break; }
            }
        }
        void Countries(float x)
        {
            Label(x, bottom + 11, 380, "PAÍSES", RtsSkin.Small);
            for (int i = 0; i < MapLayout.Countries.Length; i++)
            {
                var cities = session.Towns.Where(t => t.State.Country == i).ToArray();
                int owned = cities.Count(t => t.State.Owner == 0); int owner = session.Economy.CountryOwner(i);
                int potential = cities.Sum(t => t.PotentialIncome);
                var country = MapLayout.Countries[i];
                var r = new Rect(x, bottom + 32 + i * 24, 380, 22);
                if (GUI.Button(r, GUIContent.none, RtsSkin.Button))
                {
                    var town = cities.FirstOrDefault(t => t.State.Owner != 0) ?? cities[0]; controller.SelectTown(town); controller.Focus(town.transform.position);
                }
                Label(x + 8, r.y + 1, 150, country.Name, RtsSkin.Tiny);
                Label(x + 158, r.y + 1, 45, owned + "/" + cities.Length, RtsSkin.Tiny);
                string status = owner == 0 ? "+" + potential + " oro" : owner == 1 ? "enemigo +" + potential : "potencial +" + potential;
                Label(x + 204, r.y + 1, 120, status, RtsSkin.Tiny);
                for (int c = 0; c < cities.Length; c++) RtsSkin.Fill(new Rect(r.xMax - 35 + c * 16, r.y + 5, 11, 11), VisualFactory.TeamColor(cities[c].State.Owner));
                if (r.Contains(MousePoint)) tooltip = country.Name + "\n" + (owner == 0 ? "+" + potential + " oro por ronda" : "Potencial: +" + potential + " oro por ronda") + ". Al completarlo: +" + country.PerTurn + " " + BattleRules.Name(country.Reinforcement) + " cada 60 s (máx " + country.PerTurn * 5 + " vivos); las bajas reponen el cupo. La región completa añade +" + session.Economy.RegionBonuses[country.Region] + " oro por ronda.";
            }
            Label(x, bottom + 181, 390, "SHIFT: ENCOLAR · CTRL + 1–9: GRUPOS", RtsSkin.Tiny);
        }
        void DrawWorld()
        {
            if(NavalWorld.Current)foreach(var harbor in NavalWorld.Current.Harbors)
            {
                if(controller.SelectedHarbor!=harbor&&harbor.CaptureProgress<=0&&!controller.ShowHealthBars)continue;
                var hp=cam.WorldToScreenPoint(harbor.Landing+Vector3.up*4.8f)/Scale;float hy=height-hp.y;if(hp.z<=0||hy<69||hy>bottom-20)continue;
                var hr=new Rect(hp.x-92,hy,184,24);RtsSkin.Fill(hr,new Color(.025f,.035f,.025f,.86f));Text(hr,harbor.DisplayName,RtsSkin.Center);
                if(harbor.CaptureProgress>0)RtsSkin.Bar(new Rect(hp.x-65,hy+27,130,7),harbor.CaptureProgress,VisualFactory.TeamColor(harbor.State.Capturing));
            }
            Settlement hoveredTown = null;
            var townRay = cam.ScreenPointToRay(controller.Pointer);
            foreach (var hit in Physics.RaycastAll(townRay, 250, ~0, QueryTriggerInteraction.Collide))
            { hoveredTown = hit.collider.GetComponentInParent<Settlement>(); if (hoveredTown) break; }
            foreach (var town in session.Towns)
            {
                bool visible = town.Selected || hoveredTown == town || controller.ShowHealthBars;
                Vector3 p = cam.WorldToScreenPoint(town.transform.position + Vector3.up * 4.8f) / Scale;
                float y = height - p.y; if (p.z <= 0 || y < 69 || y > bottom - 20) continue;
                if (!visible) continue;
                var r = new Rect(p.x - 88, y - 3, 176, 25); RtsSkin.Fill(r, new Color(.025f,.035f,.025f,.86f));
                Text(r, town.DisplayName, new GUIStyle(RtsSkin.Center){fontSize=12,normal={textColor=Color.Lerp(VisualFactory.TeamColor(town.State.Owner),Color.white,.55f)}});
                if (town.State.Capture > 0 || town.State.Contested)
                {
                    RtsSkin.Bar(new Rect(p.x - 65, y + 25, 130, 7), town.Defender?town.Defender.Health/town.Defender.MaxHealth:0, town.State.Contested ? RtsSkin.Gold : VisualFactory.TeamColor(town.State.Capturing));
                    if(town.State.Contested)Label(p.x-65,y+33,150,"DEFENSOR EN COMBATE",RtsSkin.Tiny);
                }
            }
            foreach (var target in session.Targets)
            {
                bool selected = target is Soldier soldier && soldier.Selected || target is Ship ship && ship.Selected;
                if (!target.IsAlive || (!controller.ShowHealthBars && !selected && target != controller.Hovered && target.Health >= target.MaxHealth)) continue;
                var p = cam.WorldToScreenPoint(target.transform.position + Vector3.up * (target is DefenseTower || target is Ship ? 4.8f : 1.5f)) / Scale;
                float y = height - p.y; if(p.z<=0||y<66||y>bottom-8)continue;
                float size = target is DefenseTower || target is Ship ? 44 : 18;
                RtsSkin.Bar(new Rect(p.x-size/2,y,size,5),target.Health/target.MaxHealth,target.Team==0?new Color(.5f,.88f,.32f):new Color(.94f,.24f,.14f));
            }
        }
        void DrawMinimap(Rect r)
        {
            RtsSkin.Frame(new Rect(r.x-3,r.y-3,r.width+6,r.height+6));
            if (!minimapTexture) { minimapTexture = new Texture2D(48,37,TextureFormat.RGBA32,false); for (int ix=0;ix<48;ix++) for(int iz=0;iz<37;iz++){float x=Mathf.Lerp(-MapLayout.HalfWidth,MapLayout.HalfWidth,(ix+.5f)/48f),z=Mathf.Lerp(-MapLayout.HalfDepth,MapLayout.HalfDepth,(iz+.5f)/37f);float h=MapLayout.Height(x,z); minimapTexture.SetPixel(ix,iz,MapLayout.IsLand(x,z)?Color.Lerp(new Color(.24f,.38f,.20f),new Color(.56f,.63f,.30f),Mathf.Clamp01(h/6.2f)):new Color(.10f,.25f,.34f));} minimapTexture.Apply(); minimapTexture.filterMode=FilterMode.Point; }
            GUI.DrawTexture(r,minimapTexture,ScaleMode.StretchToFill,false);
            foreach(var town in session.Towns)
            {
                var p=MapPoint(town.transform.position,r);RtsSkin.Fill(new Rect(p.x-4,p.y-4,8,8),VisualFactory.TeamColor(town.State.Owner));
                if(town.Defense.IsAlive)Outline(new Rect(p.x-6,p.y-6,12,12),new Color(.83f,.76f,.48f));
            }
            foreach(var unit in session.Units) {if(!unit||!unit.IsAlive)continue;var p=MapPoint(unit.transform.position,r);RtsSkin.Fill(new Rect(p.x-1,p.y-1,2.5f,2.5f),unit.Selected?Color.white:VisualFactory.TeamColor(unit.Team));}
            if(NavalWorld.Current)
            {
                foreach(var harbor in NavalWorld.Current.Harbors){var p=MapPoint(harbor.Landing,r);Outline(new Rect(p.x-3,p.y-3,6,6),VisualFactory.TeamColor(harbor.Owner));}
                foreach(var ship in NavalWorld.Current.Ships){if(!ship||!ship.IsAlive)continue;var p=MapPoint(ship.transform.position,r);RtsSkin.Fill(new Rect(p.x-2,p.y-2,4,4),ship.Selected?Color.white:VisualFactory.TeamColor(ship.Team));}
            }
            Vector2[] corners={new Vector2(0,BottomPixels),new Vector2(Screen.width,BottomPixels),new Vector2(Screen.width,Screen.height-TopPixels),new Vector2(0,Screen.height-TopPixels)};
            GUI.BeginGroup(r);
            for(int c=0;c<4;c++)
            {
                var a=MapPoint(controller.CameraRig.Ground(corners[c]),r)-r.position;
                var b=MapPoint(controller.CameraRig.Ground(corners[(c+1)%4]),r)-r.position;
                Matrix4x4 matrix=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);
                RtsSkin.Fill(new Rect(a.x,a.y,(b-a).magnitude,1),Color.white);GUI.matrix=matrix;
            }
            GUI.EndGroup();
            var e=Event.current;
            if(e.type==EventType.MouseDown&&r.Contains(MousePoint)&&!controller.HelpVisible)
            {
                float px=Mathf.Lerp(-MapLayout.HalfWidth,MapLayout.HalfWidth,(MousePoint.x-r.x)/r.width), pz=Mathf.Lerp(MapLayout.HalfDepth,-MapLayout.HalfDepth,(MousePoint.y-r.y)/r.height); Vector3 point=MapLayout.Point(px,pz);
                if(e.button==0&&controller.OrderCursor)controller.OrderAt(point,controller.AttackCursor);
                else if(e.button==0)controller.Focus(point);else if(e.button==1)controller.OrderAt(point,controller.AttackCursor);e.Use();
            }
        }
        static Vector2 MapPoint(Vector3 p,Rect r)=>new Vector2(r.x+(p.x+MapLayout.HalfWidth)/(MapLayout.HalfWidth*2)*r.width,r.y+(MapLayout.HalfDepth-p.z)/(MapLayout.HalfDepth*2)*r.height);
        static void Outline(Rect r,Color c) {RtsSkin.Fill(new Rect(r.x,r.y,r.width,1),c);RtsSkin.Fill(new Rect(r.x,r.yMax,r.width,1),c);RtsSkin.Fill(new Rect(r.x,r.y,1,r.height),c);RtsSkin.Fill(new Rect(r.xMax,r.y,1,r.height),c);}
        void Help()
        {
            RtsSkin.Fill(new Rect(0,64,width,height-64),new Color(0,0,0,.7f));var r=new Rect(width/2-410,height/2-339,820,678);RtsSkin.Frame(r,RtsSkin.Gold);
            Label(r.x+26,r.y+21,760,"CONQUISTA · RECLUTA · FORTIFICA",RtsSkin.Title);
            string[] lines={"Clic/caja selecciona. Shift añade. Doble clic elige el mismo tipo en pantalla.","Clic derecho ataca enemigos, sigue aliados o mueve al terreno. A + suelo avanza combatiendo.","S detiene y reacciona. H mantiene posición. P + destino patrulla. E selecciona tu ejército.","En ciudades azules: Q espadachín, W ballestero, D guardia, F mago, R mortero; C sanador.","U mejora a nivel II: +6 oro/ronda si controlas el país; cuesta 90 oro.","Captura: derrota al defensor y entra en el pequeño círculo. T: torre por 60 oro.","La cola muestra hasta 5 compras; pulsa un encargo para cancelarlo y recuperar el oro.","Cada 60 s: base 12 + ciudades de países completos + bonus por regiones completas.","Cada país completo da refuerzos gratis, hasta 5 oleadas vivas; las bajas reponen el cupo.","Conquista: controla " + session.VictoryTarget + " ciudades durante 20 s. Capitales: captura la capital enemiga.","Rueda: zoom suave. Botón central/flechas: cámara. Retroceso: vista inicial. Espacio: centrar.","Ctrl+1–9 guarda grupos; doble pulsación 1–9 centra. Alt muestra vidas. F10 pausa."};
            for(int i=0;i<lines.Length;i++)Label(r.x+26,r.y+66+i*25,775,lines[i],RtsSkin.Small);
            Label(r.x+26,r.y+378,300,"VELOCIDAD DE CÁMARA",RtsSkin.Small);
            controller.CameraRig.PanSpeed=GUI.HorizontalSlider(new Rect(r.x+239,r.y+388,210,20),controller.CameraRig.PanSpeed,.5f,2.2f);
            controller.EdgePan=GUI.Toggle(new Rect(r.x+490,r.y+381,290,27),controller.EdgePan,"Cámara por los bordes");
            Label(r.x+26,r.y+421,195,"IA SIGUIENTE PARTIDA",RtsSkin.Small);
            BattleSession.DifficultyForNewMatch=(BattleSession.AiDifficulty)GUI.SelectionGrid(new Rect(r.x+228,r.y+418,558,33),(int)BattleSession.DifficultyForNewMatch,new[]{"Tranquila / ataque tras 2 min","Estándar"},2,RtsSkin.Button);
            Label(r.x+26,r.y+469,200,"SIGUIENTE PARTIDA",RtsSkin.Small);
            BattleSession.LayoutForNewMatch=(BattleSession.StartLayout)GUI.SelectionGrid(new Rect(r.x+228,r.y+466,558,33),(int)BattleSession.LayoutForNewMatch,new[]{"Reparto Risk","Países iniciales","Práctica"},3,RtsSkin.Button);
            Label(r.x+26,r.y+513,90,"Semilla",RtsSkin.Small);
            seedText=GUI.TextField(new Rect(r.x+100,r.y+511,170,30),seedText,11);
            if(Button(new Rect(r.x+282,r.y+509,130,34),"Otra semilla")){BattleSession.NewSeed();seedText=BattleSession.SeedForNewMatch.ToString();}
            Text(new Rect(r.x+432,r.y+509,356,45),"Reparto Risk: ciudades al azar. Países iniciales: un país por bando y el resto neutral.",new GUIStyle(RtsSkin.Small){wordWrap=true});
            Label(r.x+26,r.y+556,760,"Partida actual: "+session.LayoutName+" · semilla "+session.Seed,RtsSkin.Tiny);
            Label(r.x+26,r.y+579,775,"NAVAL · F3 puerto · Q/W barcos · N flota · B embarcar · D desembarcar · isla: +8 oro/ronda",RtsSkin.Tiny);
            if(Button(new Rect(r.x+26,r.y+602,160,44),"Volver"))controller.HelpVisible=false;
            if(Button(new Rect(r.x+199,r.y+602,220,44),"Empezar Conquista"))StartMatch(BattleSession.VictoryMode.Conquest);
            if(Button(new Rect(r.x+432,r.y+602,220,44),"Empezar Capitales"))StartMatch(BattleSession.VictoryMode.Capitals);
        }
        void StartMatch(BattleSession.VictoryMode mode)
        {
            if(!int.TryParse(seedText,out int seed)){session.Message("Escribe una semilla numérica válida.");return;}
            BattleSession.SeedForNewMatch=seed;BattleSession.ModeForNewMatch=mode;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        void Result()
        {
            RtsSkin.Fill(new Rect(0,0,width,height),new Color(0,0,0,.7f));var r=new Rect(width/2-270,height/2-135,540,270);RtsSkin.Frame(r,RtsSkin.Gold);
            Text(new Rect(r.x+20,r.y+30,500,55),session.Winner==0?"VICTORIA":"DERROTA",new GUIStyle(RtsSkin.Center){fontSize=34});
            Text(new Rect(r.x+20,r.y+104,500,35),"Las Marcas tienen un nuevo estandarte.",RtsSkin.Center);
            if(Button(new Rect(r.x+165,r.y+177,210,48),"Nueva partida")){BattleSession.ModeForNewMatch=session.Mode;BattleSession.NewSeed();SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);}
        }
    }
}

