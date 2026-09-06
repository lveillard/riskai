using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiskAI
{
    public sealed partial class BattleHud : MonoBehaviour
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
        readonly HudSnapshot hud = new HudSnapshot();
        long lastHudTick = -1;
        bool hudDirty = true;
        int countryPage;
        Vector2 MousePoint => new Vector2(controller.Pointer.x / Scale, (Screen.height - controller.Pointer.y) / Scale);
        public void Initialize(BattleSession battle, RtsController input, Camera camera)
        {
            session = battle; controller = input; cam = camera;
            seedText=session.Seed.ToString();
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind))) portraits[kind] = Resources.Load<Texture2D>("Portraits/" + BattleRules.Model(kind));
            RefreshHudSnapshot();OpenInitialMenu();
        }
        void LateUpdate()
        {
            if (!session || session.Clock == null) return;
            if (!hudDirty && lastHudTick == session.Clock.TickCount) return;
            RefreshHudSnapshot();
        }
        void RefreshHudSnapshot()
        {
            if (!session) return;
            hud.Rebuild(session);
            lastHudTick = session.Clock != null ? session.Clock.TickCount : -1;
            hudDirty = false;
        }
        void MarkHudDirty() { hudDirty = true; }
        void OnDestroy()
        {
            if(minimapTexture)Destroy(minimapTexture);
        }
        static void Text(Rect r, string text, GUIStyle style = null) => GUI.Label(r, text, style ?? RtsSkin.Text);
        static void Label(float x, float y, float w, string text, GUIStyle style = null) => Text(new Rect(x, y, w, 26), text, style);
        bool Button(Rect rect, string text, string hint = null)
        {
            if (hint != null && rect.Contains(MousePoint)) tooltip = hint;
            bool clicked = GUI.Button(rect, text, RtsSkin.Button);
            if (clicked) MarkHudDirty();
            return clicked;
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
            Label(22, 7, 270, "RISKAI · DOMINIOS", RtsSkin.Title);
            Label(23, 30, 235, "V"+Application.version+" · "+MapLayout.MapName.ToUpperInvariant(), RtsSkin.Tiny);
            Label(270, 13, 180, hud.Gold + " ORO  +" + hud.Income + "/RONDA", RtsSkin.Small);
            string population=MapLayout.IsImported
                ? "MÓV. "+hud.MobilePopulation0+" / 100 · G "+hud.GarrisonPopulation0+"  | IA "+hud.MobilePopulation1+" / 100 · G "+hud.GarrisonPopulation1
                : hud.Population0 + " TROPAS / IA " + hud.Population1;
            Label(455, 13, 310, population, RtsSkin.Small);
            Label(770, 13, 170, "RONDA " + hud.Round + " · " + Mathf.CeilToInt(BattleRules.RoundSeconds - hud.RoundElapsed) + " s", RtsSkin.Small);
            Label(945, 13, 245, hud.OwnedTowns + " / " + MapLayout.Towns.Length + " CIUDADES", RtsSkin.Small);
            Label(1200,13,190,"IA "+session.DifficultyName+(session.BattleTime<session.AiFirstOffensiveTime?" / prepara":""),RtsSkin.Small);
            if (Button(new Rect(width - 195, 13, 86, 35), session.Paused ? "Continuar" : "Pausa", "F10 · pausar o continuar")) session.TogglePause();
            if (Button(new Rect(width - 101, 13, 81, 35), "Menú", "F1 · partida, controles y ajustes")) controller.HelpVisible = !controller.HelpVisible;
            RtsSkin.Frame(new Rect(0, bottom, width, 208));
            DrawMinimap(new Rect(16, bottom + 23, 208, 156));
            Label(20, bottom + 181, 205, "CLIC: CÁMARA · DER.: ORDEN", RtsSkin.Tiny);
            float actions = width - 660;
            if(controller.SelectedCamp)CampDetails(controller.SelectedCamp,actions);
            else if (controller.SelectedHarbor) HarborDetails(controller.SelectedHarbor,actions);
            else if(controller.Fleet.Count>0)FleetDetails(actions);
            else if (controller.SelectedTown) { TownDetails(controller.SelectedTown); Shop(controller.SelectedTown, actions); }
            else if (controller.Selection.Count > 0) { ArmyDetails(actions); Orders(actions); }
            else if (controller.InspectedTarget) { TargetDetails(controller.InspectedTarget); Orders(actions); }
            else
            {
                Label(255, bottom + 23, 650, "ELIGE UNA CIUDAD O UNA HOGUERA", RtsSkin.Title);
                Label(255, bottom + 62, 650, "Selecciona tus soldados con una caja o pulsa E para elegir tu ejército.");
                Label(255, bottom + 91, 650, "Selecciona una ciudad azul para comprar tropas; una hoguera muestra su grupo.", RtsSkin.Small);
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
            Label(253,bottom+50,620,harbor.Owner==0?"PUERTO ALIADO":harbor.Owner==1?"PUERTO ENEMIGO":"PUERTO NEUTRAL",RtsSkin.Small);
            Label(253,bottom+80,625,harbor.IsImportedPort?"Ciudad portuaria · cuenta para su grupo, ingresos y conquista.":harbor.IsIsland?"Puerto insular: desembarca y ocupa su círculo.":"Puerto independiente · producción naval y punto de desembarco.",RtsSkin.Small);
            Label(253,bottom+103,650,ClaimText(harbor.State,harbor.Defender),RtsSkin.Tiny);
            Label(253,bottom+123,610,"Cola naval: "+harbor.QueueCount+" / 3 · pulsa un encargo para cancelarlo",RtsSkin.Tiny);
            for(int i=0;i<harbor.QueueCount;i++)
            {
                var r=new Rect(254+i*125,bottom+148,118,42);if(Button(r,harbor.QueuedKind(i)==ShipKind.Galley?"Fragata":"Transporte"))controller.Feedback(harbor.CancelTraining(i));
                if(i==0)RtsSkin.Bar(new Rect(r.x,r.yMax+3,r.width,5),harbor.TrainingProgress,RtsSkin.Gold);
            }
            Label(x,bottom+16,590,"ASTILLERO",RtsSkin.Title);
            bool enabled=GUI.enabled;GUI.enabled=harbor.Owner==0&&!session.Paused;
            ShipProfile galley=Harbor.Profile(ShipKind.Galley),transport=Harbor.Profile(ShipKind.Transport);
            if(Button(new Rect(x,bottom+51,220,62),"[Q] "+galley.Name+" · "+galley.Cost+" oro",galley.Health+" vida · "+AttackName(galley.Attack).ToLowerInvariant()+" "+galley.Damage+" · alcance "+galley.Range+" · armadura "+galley.Armor+" · "+galley.TrainSeconds+" s"))controller.BuyShip(ShipKind.Galley);
            if(Button(new Rect(x+231,bottom+51,220,62),"[W] "+transport.Name+" · "+transport.Cost+" oro",transport.Health+" vida · "+transport.Capacity+" soldados · sin ataque · armadura "+transport.Armor+" · "+transport.TrainSeconds+" s"))controller.BuyShip(ShipKind.Transport);
            GUI.enabled=enabled;
            Label(x,bottom+133,225,"TORRE PERMANENTE",RtsSkin.Small);
            Label(x,bottom+155,225,"Protege a su guarnición",RtsSkin.Tiny);
            if(Button(new Rect(x+231,bottom+126,220,44),"[N] Seleccionar flota"))controller.SelectFleet();
            Label(x,bottom+182,590,"Clic derecho en un muelle: navegar y desembarcar",RtsSkin.Tiny);
        }
        void FleetDetails(float x)
        {
            Ship ship=null;int alive=0,cargo=0,capacity=0;
            foreach(var candidate in controller.Fleet)
            {
                if(!candidate||!candidate.IsAlive)continue;
                if(ship==null)ship=candidate;alive++;cargo+=candidate.CargoCount;
                if(candidate.Kind==ShipKind.Transport)capacity+=candidate.Profile.Capacity;
            }
            if(!ship)return;
            Label(253,bottom+18,600,alive==1?ship.DisplayName:"FLOTA · "+alive+" BARCOS",RtsSkin.Title);
            Label(253,bottom+50,620,Mathf.CeilToInt(ship.Health)+" / "+ship.MaxHealth+" VIDA · "+ship.OrderLabel,RtsSkin.Small);
            RtsSkin.Bar(new Rect(254,bottom+83,255,8),ship.Health/ship.MaxHealth,new Color(.4f,.8f,.4f));
            ShipProfile profile=ship.Profile;
            Label(253,bottom+105,650,capacity>0?"TROPAS EMBARCADAS · "+cargo+" / "+capacity:profile.Damage+" DAÑO "+AttackName(profile.Attack).ToUpperInvariant()+" · "+profile.Range+" ALCANCE · "+profile.Armor+" ARMADURA",RtsSkin.Small);
            Label(253,bottom+137,630,"B: embarca tropas junto al muelle. D: desembarca cerca del puerto.",RtsSkin.Small);
            Label(253,bottom+171,650,"Clic derecho en un puerto: navegar y desembarcar allí.",RtsSkin.Tiny);
            if(Button(new Rect(x,bottom+28,190,52),"[M] Navegar"))controller.ArmMove();
            if(Button(new Rect(x+200,bottom+28,190,52),"[A] Atacar"))controller.ArmAttack();
            if(Button(new Rect(x+400,bottom+28,190,52),"[S] Detener"))controller.Stop();
            if(Button(new Rect(x,bottom+94,190,52),"[B] Embarcar"))controller.BoardNearby();
            if(Button(new Rect(x+200,bottom+94,190,52),"[D] Desembarcar"))controller.UnloadFleet();
            if(Button(new Rect(x+400,bottom+94,190,52),"[F3] Puerto"))controller.FocusHarbor();
            Label(x,bottom+176,590,"Fragata: combate naval y costa · Transporte: conquistar islas",RtsSkin.Tiny);
        }
        void Portrait(Rect r, UnitKind kind, Color border)
        {
            RtsSkin.Frame(r, border);
            if (portraits.TryGetValue(kind, out var image) && image) GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), image, ScaleMode.ScaleToFit);
        }
        void TownDetails(Settlement town)
        {
            string owner = town.State.Owner == 0 ? "ALIANZA DEL ALBA" : town.State.Owner == 1 ? "FRONTERA CARMESÍ" : "CIUDAD NEUTRAL";
            Label(252, bottom + 15, 670, town.DisplayName, RtsSkin.Title);
            Label(253, bottom + 45, 640, owner + "   /   +" + hud.IncomeFor(town) + " ORO POR RONDA", RtsSkin.Small);
            var country = town.State.Country >= 0 && town.State.Country < MapLayout.Countries.Length ? MapLayout.Countries[town.State.Country] : default(MapLayout.Country);
            CountrySnapshot countrySnapshot = town.State.Country >= 0 && town.State.Country < hud.Countries.Length ? hud.Countries[town.State.Country] : null;
            string countryLine = countrySnapshot != null ? country.Name + "  ·  " + countrySnapshot.Owned + "/" + countrySnapshot.CityCount + "  ·  " + (countrySnapshot.Owner == 0 ? "+" + countrySnapshot.PotentialIncome + " oro/ronda · " + country.PerTurn + " " + BattleRules.Name(country.Reinforcement) + "/60 s (máx " + countrySnapshot.CityCount * 5 + " puntos)" : "Completa el país para cobrar") : "Completa el país para cobrar";
            Label(253, bottom + 73, 680, countryLine, RtsSkin.Small);
            Label(253, bottom + 97, 650, ClaimText(town.State, town.Defender), RtsSkin.Tiny);
            Label(253,bottom+121,640,"TORRE PERMANENTE · protege a su defensor",RtsSkin.Tiny);
            Label(253, bottom + 149, 490, town.QueueCount > 0 ? "RECLUTANDO · PULSA UN ENCARGO PARA CANCELAR" : "COLA DE RECLUTAMIENTO VACÍA", RtsSkin.Tiny);
            for (int i = 0; i < 5; i++)
            {
                var rect = new Rect(254 + i * 58, bottom + 164, 50, 36); RtsSkin.Frame(rect);
                if (i >= town.QueueCount) continue;
                UnitKind kind = town.QueuedKind(i); Portrait(rect, kind, i == 0 ? RtsSkin.Gold : new Color(.5f,.5f,.4f));
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) { MarkHudDirty(); controller.Feedback(town.CancelTraining(i)); }
                if (rect.Contains(MousePoint)) tooltip = "Cancelar " + BattleRules.Name(kind) + " · devuelve " + BattleRules.Cost(kind) + " de oro.";
                if (i == 0) RtsSkin.Bar(new Rect(rect.x + 2, rect.yMax - 7, rect.width - 4, 5), town.TrainingProgress, RtsSkin.Gold);
            }
            if (town.QueueCount > 0) Label(565, bottom + 182, 330, BattleRules.Name(town.TrainingKind) + " · " + Mathf.CeilToInt((1 - town.TrainingProgress) * BattleRules.TrainTime(town.TrainingKind)) + " s", RtsSkin.Small);
        }
        void Shop(Settlement town, float x)
        {
            Label(x, bottom + 15, 320, "RECLUTAR UNIDADES", RtsSkin.Small);
            for (int i = 0; i < 6; i++) UnitCard(new Rect(x + (i % 3) * 108, bottom + 43 + (i / 3) * 78, 102, 72), (UnitKind)i, town);
            float buildX=x+335;
            var fort=ReforgedProfiles.CapturableTower;
            Label(buildX,bottom+15,300,"DEFENSA DEL EDIFICIO",RtsSkin.Small);
            Label(buildX,bottom+52,305,town.Defender?"TORRE PROTEGIDA":"TORRE SIN GUARNICIÓN",RtsSkin.Small);
            Label(buildX,bottom+80,305,fort.MinimumDamage+"–"+fort.MaximumDamage+" daño / "+fort.Cooldown+" s · alcance "+fort.Range,RtsSkin.Tiny);
            Label(buildX,bottom+107,305,"Permanente · cambia con su defensor",RtsSkin.Tiny);
            if(town.State.Country>=0&&town.State.Country<session.Camps.Count&&Button(new Rect(buildX,bottom+137,306,48),"VER GRUPO Y HOGUERA"))
            {var camp=session.Camps[town.State.Country];controller.SelectCamp(camp);if(camp)controller.Focus(camp.SpawnPoint);}
        }

        void UnitCard(Rect r, UnitKind kind, Settlement town)
        {
            bool available = town.State.Owner == 0 && town.State.Level >= BattleRules.RequiredLevel(kind);
            bool affordable = hud.Gold >= BattleRules.Cost(kind);
            bool previous = GUI.enabled; GUI.enabled = previous && available && affordable && !session.Paused && town.QueueCount < 5;
            if (GUI.Button(r, GUIContent.none, RtsSkin.Button)) { MarkHudDirty(); controller.Recruit(kind); }
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
                ShipProfile profile=ship.Profile;
                Label(253,bottom+23,590,ship.DisplayName+" · FLOTA ENEMIGA",RtsSkin.Title);
                Label(253,bottom+61,590,Mathf.CeilToInt(ship.Health)+" / "+profile.Health+" vida · armadura "+profile.Armor,RtsSkin.Small);
                Label(253,bottom+95,590,ship.Kind==ShipKind.Galley?profile.Damage+" daño normal cada "+profile.Cooldown+" s · alcance "+profile.Range:"Transporte · "+profile.Capacity+" plazas · sin ataque",RtsSkin.Small);return;
            }
            if(target is DefenseTower tower)
            {
                var profile = ReforgedProfiles.CapturableTower;
                Label(253,bottom+23,590,"TORRE DE GUARDIA",RtsSkin.Title);
                Label(253,bottom+61,590,"Permanente · cambia de bando con su guarnición",RtsSkin.Small);
                Label(253,bottom+95,590,profile.MinimumDamage+"–"+profile.MaximumDamage+" daño perforante cada "+profile.Cooldown+" s · alcance "+profile.Range,RtsSkin.Small);
                Label(253,bottom+129,590,"Ataca al defensor del círculo para conquistar el edificio.",RtsSkin.Small);
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
            string[] names = { "Mover", "Detener", "Atacar", "Mantener", "Patrullar", "Ejército", "Centrar", "Vista", "Base inicial" };
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
            const int rowsPerPage=6;
            int pages=Mathf.Max(1,Mathf.CeilToInt(MapLayout.Countries.Length/(float)rowsPerPage));countryPage=Mathf.Clamp(countryPage,0,pages-1);
            int first=countryPage*rowsPerPage,last=Mathf.Min(MapLayout.Countries.Length,first+rowsPerPage);
            Label(x, bottom + 11, 260,"PAÍSES · "+(first+1)+"–"+last+" / "+MapLayout.Countries.Length, RtsSkin.Small);
            bool enabled=GUI.enabled;GUI.enabled=countryPage>0;if(Button(new Rect(x+300,bottom+8,34,25),"‹"))countryPage--;GUI.enabled=enabled;
            enabled=GUI.enabled;GUI.enabled=countryPage<pages-1;if(Button(new Rect(x+340,bottom+8,34,25),"›"))countryPage++;GUI.enabled=enabled;
            for (int i = first; i < last; i++)
            {
                CountrySnapshot snapshot = hud.Countries[i];
                int owned = snapshot.Owned; int owner = snapshot.Owner;
                int potential = snapshot.PotentialIncome;
                var country = MapLayout.Countries[i];
                var r = new Rect(x, bottom + 32 + (i-first) * 24, 380, 22);
                if (GUI.Button(r, GUIContent.none, RtsSkin.Button))
                {
                    Settlement town = null;
                    for (int city = 0; city < snapshot.CityCount; city++)
                        if (snapshot.Cities[city] && snapshot.Cities[city].State.Owner != 0) { town = snapshot.Cities[city]; break; }
                    if (!town && snapshot.CityCount > 0) town = snapshot.Cities[0];
                    if (town) { MarkHudDirty(); controller.SelectTown(town); controller.Focus(town.transform.position); }
                }
                Label(x + 8, r.y + 1, 145, country.Name, RtsSkin.Tiny);
                Label(x + 153, r.y + 1, 42, owned + "/" + snapshot.CityCount, RtsSkin.Tiny);
                string status = owner == 0 ? "+" + potential + " oro" : owner == 1 ? "rival +" + potential : "potencial +" + potential;
                Label(x + 195, r.y + 1, 118, status, RtsSkin.Tiny);
                int markers=Mathf.Min(snapshot.CityCount,8);
                for (int c = 0; c < markers; c++) RtsSkin.Fill(new Rect(r.xMax-8-markers*6+c*6,r.y+7,4,4),VisualFactory.TeamColor(snapshot.Cities[c].State.Owner));
                if (r.Contains(MousePoint)) tooltip = country.Name + "\n" + (owner == 0 ? "+" + potential + " oro por ronda" : "Potencial: +" + potential + " oro por ronda") + ". Al completarlo: +" + country.PerTurn + " " + BattleRules.Name(country.Reinforcement) + " cada 60 s (máx " + snapshot.CityCount * 5 + " puntos vivos); las bajas liberan capacidad para nuevos refuerzos.";
            }
            Label(x, bottom + 181, 390, "SHIFT: ENCOLAR · CTRL + 1–9: GRUPOS", RtsSkin.Tiny);
        }
        void DrawWorld()
        {
            if(NavalWorld.Current)foreach(var harbor in NavalWorld.Current.Harbors)
            {
                bool capturing=harbor.CaptureProgress>0&&harbor.CaptureProgress<1&&harbor.State.Capturing>=0;
                if(controller.SelectedHarbor!=harbor&&!capturing&&!harbor.State.Contested&&!controller.ShowHealthBars)continue;
                var hp=cam.WorldToScreenPoint(harbor.Landing+Vector3.up*4.8f)/Scale;float hy=height-hp.y;if(hp.z<=0||hy<69||hy>bottom-20)continue;
                var hr=new Rect(hp.x-92,hy,184,24);RtsSkin.Fill(hr,new Color(.025f,.035f,.025f,.86f));Text(hr,harbor.DisplayName,RtsSkin.Center);
                if(capturing)
                {
                    RtsSkin.Bar(new Rect(hp.x-65,hy+27,130,7),harbor.CaptureProgress,VisualFactory.TeamColor(harbor.State.Capturing));
                }
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
                // Succession is immediate; nearby enemies or a bound guard are not a progress bar.
                if (town.State.Capture > 0 && town.State.Capture < 1 && town.State.Capturing >= 0)
                {
                    RtsSkin.Bar(new Rect(p.x - 65, y + 25, 130, 7), town.State.Capture, VisualFactory.TeamColor(town.State.Capturing));
                    Label(p.x-65,y+33,170,"CONVERSIÓN "+Mathf.RoundToInt(town.State.Capture*100)+"%",RtsSkin.Tiny);
                }
            }
            foreach (var target in session.Targets)
            {
                if (target is DefenseTower) continue; // Permanent buildings have no destructible health bar.
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
            if (!minimapTexture) { const int resolution=192; minimapTexture = new Texture2D(resolution,resolution,TextureFormat.RGBA32,false); for (int ix=0;ix<resolution;ix++) for(int iz=0;iz<resolution;iz++){float x=Mathf.Lerp(-MapLayout.HalfWidth,MapLayout.HalfWidth,(ix+.5f)/resolution),z=Mathf.Lerp(-MapLayout.HalfDepth,MapLayout.HalfDepth,(iz+.5f)/resolution);float h=MapLayout.Height(x,z); minimapTexture.SetPixel(ix,iz,MapLayout.IsLand(x,z)?Color.Lerp(new Color(.24f,.38f,.20f),new Color(.56f,.63f,.30f),Mathf.Clamp01(h/6.2f)):new Color(.10f,.25f,.34f));} minimapTexture.Apply(); minimapTexture.filterMode=FilterMode.Point; }
            GUI.DrawTexture(r,minimapTexture,ScaleMode.StretchToFill,false);
            foreach(var town in session.Towns)
            {
                float marker=MapLayout.IsImported?3:8;
                var p=MapPoint(town.transform.position,r);RtsSkin.Fill(new Rect(p.x-marker*.5f,p.y-marker*.5f,marker,marker),VisualFactory.TeamColor(town.State.Owner));
                if(town.Defense.IsAlive)Outline(new Rect(p.x-marker*.5f-2,p.y-marker*.5f-2,marker+4,marker+4),new Color(.83f,.76f,.48f));
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
        static string ClaimText(TownState state, Soldier defender)
        {
            if (state == null) return "";
            if (state.Contested) return "GUARNICIÓN EN COMBATE · el aliado cercano tiene prioridad";

            if (defender) return defender.IsGarrison ? "GUARNICIÓN BLOQUEADA · no mover / no embarcar" : "DEFENSOR EN CÍRCULO 1,55 m";
            return "CÍRCULO 1,55 m · relevo aliado a 4,43 m tiene prioridad";
        }
        sealed class HudSnapshot
        {
            public int Gold;
            public int Income;
            public int Population0;
            public int Population1;
            public int MobilePopulation0;
            public int MobilePopulation1;
            public int GarrisonPopulation0;
            public int GarrisonPopulation1;
            public int OwnedTowns;
            public int Round;
            public float RoundElapsed;
            public readonly CountrySnapshot[] Countries = new CountrySnapshot[MapLayout.Countries.Length];
            public HudSnapshot()
            {
                for (int i = 0; i < Countries.Length; i++) Countries[i] = new CountrySnapshot();
            }
            public int IncomeFor(Settlement town)
            {
                if (!town || town.State.Owner < 0) return 0;
                if (town.State.Country < 0) return town.PotentialIncome;
                return town.State.Country < Countries.Length && Countries[town.State.Country].Owner == town.State.Owner ? town.PotentialIncome : 0;
            }
            public void Rebuild(BattleSession battle)
            {
                Gold = battle.Economy.Gold[0];
                Income = battle.Economy.Income(0);
                Population0 = battle.Population(0);
                Population1 = battle.Population(1);
                MobilePopulation0 = battle.RecruitmentPopulation(0);
                MobilePopulation1 = battle.RecruitmentPopulation(1);
                GarrisonPopulation0 = Population0 - MobilePopulation0;
                GarrisonPopulation1 = Population1 - MobilePopulation1;
                OwnedTowns = 0;
                Round = battle.Economy.Round;
                RoundElapsed = battle.Economy.ElapsedInRound;
                for (int i = 0; i < Countries.Length; i++) Countries[i].Reset();
                foreach (Settlement town in battle.Towns)
                {
                    if (!town || town.State == null) continue;
                    if (town.State.Owner == 0) OwnedTowns++;
                    int country = town.State.Country;
                    if (country < 0 || country >= Countries.Length) continue;
                    CountrySnapshot snapshot = Countries[country];
                    if (snapshot.CityCount >= snapshot.Cities.Length) continue;
                    snapshot.Cities[snapshot.CityCount++] = town;
                    snapshot.PotentialIncome += town.PotentialIncome;
                    if (town.State.Owner == 0) snapshot.Owned++;
                }
                for (int i = 0; i < Countries.Length; i++) Countries[i].RebuildOwner();
            }
        }
        sealed class CountrySnapshot
        {
            public readonly Settlement[] Cities = new Settlement[MapLayout.Towns.Length];
            public int CityCount;
            public int Owned;
            public int Owner = -1;
            public int PotentialIncome;
            public void Reset()
            {
                for (int i = 0; i < CityCount; i++) Cities[i] = null;
                CityCount = 0; Owned = 0; Owner = -1; PotentialIncome = 0;
            }
            public void RebuildOwner()
            {
                if (CityCount == 0) { Owner = -1; return; }
                Owner = Cities[0].State.Owner;
                for (int i = 1; i < CityCount; i++)
                    if (Owner < 0 || Cities[i].State.Owner != Owner) { Owner = -1; break; }
            }
        }
        void Result()
        {
            RtsSkin.Fill(new Rect(0,0,width,height),new Color(0,0,0,.7f));var r=new Rect(width/2-270,height/2-135,540,270);RtsSkin.Frame(r,RtsSkin.Gold);
            Text(new Rect(r.x+20,r.y+30,500,55),session.Winner==0?"VICTORIA":"DERROTA",new GUIStyle(RtsSkin.Center){fontSize=34});
            Text(new Rect(r.x+20,r.y+104,500,35),MapLayout.MapName + " tiene un nuevo estandarte.",RtsSkin.Center);
            if(Button(new Rect(r.x+165,r.y+177,210,48),"Nueva partida")){BattleSession.ModeForNewMatch=session.Mode;BattleSession.NewSeed();SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);}
        }
    }
}

