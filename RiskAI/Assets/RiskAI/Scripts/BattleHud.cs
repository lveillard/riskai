using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    public sealed partial class BattleHud : MonoBehaviour
    {
        public static float Scale => UiViewport.Scale;
        public static float BottomPixels => UiViewport.BottomPixels;
        public static float TopPixels => UiViewport.TopPixels;
        BattleSession session;
        RtsController controller;
        Camera cam;
        Texture2D minimapTexture;
        float width, height, bottom;
        readonly HudSnapshot hud = new HudSnapshot();
        readonly HashSet<int> canopyOccludedUnits = new HashSet<int>();
        float nextCanopyCheck;
        long lastHudTick = -1;
        bool hudDirty = true;
        readonly Queue<int> eliminationNotices = new Queue<int>();
        int announcedPlayer = -1;
        float announcementUntil;
        Vector2 MousePoint => new Vector2(controller.Pointer.x / Scale, (Screen.height - controller.Pointer.y) / Scale);
        public void Initialize(BattleSession battle, RtsController input, Camera camera)
        {
            session = battle; controller = input; cam = camera;
            session.PlayerEliminated += OnPlayerEliminated;
            RefreshHudSnapshot();ConfigureViewport();InitializeRetainedUi();
        }
        void LateUpdate()
        {
            if (!session || session.Clock == null) return;
            if (!StrategicMapView.Active && Time.unscaledTime >= nextCanopyCheck)
            {
                nextCanopyCheck = Time.unscaledTime + .1f;
                canopyOccludedUnits.Clear();
                foreach (var unit in session.Units)
                {
                    if (!unit || !unit.IsAlive || !unit.isActiveAndEnabled) continue;
                    var viewport = cam.WorldToViewportPoint(unit.AimPoint);
                    if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) continue;
                    if (session.Canopies.Obscures(cam.transform.position, unit.AimPoint)) canopyOccludedUnits.Add(unit.EntityId);
                }
            }
            // Purchases can change the economy between simulation ticks, including the
            // frame just before pausing. Keep this O(1) invalidation independent of time.
            if (hudDirty || hud.Gold != session.Economy.Gold[0] || lastHudTick / 2 != session.Clock.TickCount / 2) RefreshHudSnapshot();
            RefreshRetainedUi();
            RefreshWorldQueues();
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
            if (session) session.PlayerEliminated -= OnPlayerEliminated;
            if(minimapTexture)Destroy(minimapTexture);
            DisposeMinimapMarkers();
            UiViewport.ResetHudHeights();
        }
        static void Text(Rect r, string text, GUIStyle style = null) => GUI.Label(r, GameText.Localize(text), style ?? RtsSkin.Text);
        static void Label(float x, float y, float w, string text, GUIStyle style = null) => Text(new Rect(x, y, w, 26), text, style);
        void OnGUI()
        {
            if (!session || !controller) return;
            if(controller.HelpVisible||controller.ScoreboardVisible||session.Winner>=0)return;
            if (Event.current.type != EventType.Repaint) return;
            RtsSkin.Initialize(); ConfigureViewport(); width = Screen.width / Scale; height = Screen.height / Scale; bottom = (Screen.height - BottomPixels) / Scale;
            Matrix4x4 previous = GUI.matrix; GUI.matrix = Matrix4x4.Scale(Vector3.one * Scale);
            try { Draw(); } finally { GUI.matrix = previous; }
        }
        void Draw()
        {
            // Retained UI owns every interactive panel. IMGUI remains only for world-space
            // presentation, the selection rectangle and the transitional minimap renderer.
            // World labels are drawn by IMGUI after retained panels; keep them
            // from covering the start countdown's title and number.
            if(!session.IsStarting&&!session.Paused)DrawWorld();
            if (MinimapVisible)
            {
                DrawMinimap(MinimapRect());
            }
            if (controller.Dragging)
            {
                Rect real = controller.SelectionRect; var r = new Rect(real.x / Scale, real.y / Scale, real.width / Scale, real.height / Scale);
                RtsSkin.Fill(r, new Color(.4f, 1, .4f, .12f)); Outline(r, new Color(.55f, 1, .5f));
            }
            for (int i = 0; i < Mathf.Min(1, session.Messages.Count); i++)
            {
                var r = new Rect(UiViewport.SafeRect.xMin/Scale+16, bottom - 28 - i * 23, Mathf.Min(680,UiViewport.LogicalWidth-32), 22); RtsSkin.Fill(r, new Color(.035f, .04f, .03f, .83f));
                Label(r.x + 7, r.y, r.width - 12, session.Messages[i], RtsSkin.Small);
            }
            DrawEliminationNotice();
        }
        void OnPlayerEliminated(int player) => eliminationNotices.Enqueue(player);

        void DrawEliminationNotice()
        {
            if (Time.unscaledTime >= announcementUntil)
            {
                announcedPlayer = eliminationNotices.Count > 0 ? eliminationNotices.Dequeue() : -1;
                announcementUntil = Time.unscaledTime + (announcedPlayer >= 0 ? 5 : 0);
            }
            int player = announcedPlayer >= 0 ? announcedPlayer : session.IsPlayerEliminated(0) ? 0 : -1;
            if (player < 0) return;
            string message = player == 0 ? "DERROTA · Has sido eliminado. La partida continúa."
                : VisualFactory.TeamName(player) + " ha sido eliminado.";
            float noticeWidth = Mathf.Min(460, UiViewport.LogicalWidth - 24);
            var rect = new Rect(UiViewport.SafeRect.center.x / Scale - noticeWidth * .5f, TopPixels / Scale + 12, noticeWidth, 54);
            RtsSkin.Fill(rect, new Color(.035f, .04f, .03f, .95f));
            RtsSkin.Fill(new Rect(rect.x, rect.y, 4, rect.height), VisualFactory.TeamColor(player));
            Text(new Rect(rect.x + 12, rect.y + 7, rect.width - 24, rect.height - 14), message, RtsSkin.WrappedText);
        }
        void DrawWorld()
        {
            if(StrategicMapView.Active){DrawStrategicSymbols();return;}
            if(controller.SelectedCamp)DrawCountryPorts(controller.SelectedCamp.Country);
            if(NavalWorld.Current)foreach(var harbor in NavalWorld.Current.Harbors)
            {
                if(harbor.IsImportedPort)continue; // Its town already renders the shared post label.
                bool capturing=harbor.CaptureProgress>0&&harbor.CaptureProgress<1&&harbor.State.Capturing>=0;
                if(controller.SelectedHarbor!=harbor&&!capturing&&!harbor.State.Contested&&!controller.ShowHealthBars)continue;
                var hp=cam.WorldToScreenPoint(harbor.Landing+Vector3.up*4.8f)/Scale;float hy=height-hp.y;if(hp.z<=0||hy<TopPixels/Scale+20||hy>bottom-20)continue;
                DrawBuildingName(new Vector2(hp.x,hy),harbor.DisplayName,harbor.Owner);
                if(capturing)
                {
                    RtsSkin.Bar(new Rect(hp.x-65,hy+27,130,7),harbor.CaptureProgress,VisualFactory.TeamColor(harbor.State.Capturing));
                }
            }
            // A touch's last position is not a persistent mouse hover. Once a
            // building is selected, show that selection without a second stale label.
            Settlement hoveredTown = HasSelection || PlatformPresentation.TouchCapable || controller.OverHud(controller.Pointer)?null:RtsPicking.Town(session,cam,controller.Pointer);
            foreach (var town in session.Towns)
            {
                bool visible = town.Selected || hoveredTown == town || controller.ShowHealthBars;
                Vector3 p = cam.WorldToScreenPoint(town.transform.position + Vector3.up * 4.8f) / Scale;
                float y = height - p.y; if (p.z <= 0 || y < TopPixels/Scale+20 || y > bottom - 20) continue;
                if (!visible) continue;
                DrawBuildingName(new Vector2(p.x,y),town.DisplayName,town.State.Owner);
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
                bool persistentShipHealth=target is Ship;
                if (!target.IsAlive || !target.isActiveAndEnabled || (!persistentShipHealth && !controller.ShowHealthBars && !selected && target != controller.Hovered && target.Health >= target.MaxHealth && !canopyOccludedUnits.Contains(target.EntityId))) continue;
                float healthHeight=target is Soldier person?VisualMetrics.HeightFor(person.Kind)+.15f:4.8f;
                var p = cam.WorldToScreenPoint(target.transform.position + Vector3.up * healthHeight) / Scale;
                float y = height - p.y; if(p.z<=0||y<TopPixels/Scale+16||y>bottom-8)continue;
                float size = target is Ship ? 56 : 28;
                RtsSkin.WorldHealthBar(new Rect(p.x-size/2,y,size,target is Ship?9:7),target.Health/target.MaxHealth,VisualFactory.TeamColor(target.Team));
            }
        }
        void DrawMinimap(Rect r)
        {
            RtsSkin.Frame(new Rect(r.x-3,r.y-3,r.width+6,r.height+6));
            if (!minimapTexture) { const int resolution=192; minimapTexture = new Texture2D(resolution,resolution,TextureFormat.RGBA32,false); for (int ix=0;ix<resolution;ix++) for(int iz=0;iz<resolution;iz++){float x=Mathf.Lerp(MapLayout.PlayableMin.x,MapLayout.PlayableMax.x,(ix+.5f)/resolution),z=Mathf.Lerp(MapLayout.PlayableMin.y,MapLayout.PlayableMax.y,(iz+.5f)/resolution);float h=MapLayout.Height(x,z); minimapTexture.SetPixel(ix,iz,MapLayout.IsLand(x,z)?Color.Lerp(new Color(.24f,.38f,.20f),new Color(.56f,.63f,.30f),Mathf.Clamp01(h/6.2f)):new Color(.10f,.25f,.34f));} minimapTexture.Apply(); minimapTexture.filterMode=FilterMode.Point; }
            GUI.DrawTexture(r,minimapTexture,ScaleMode.StretchToFill,false);
            DrawMinimapMarkers(r);
            Vector2[] corners={new Vector2(0,BottomPixels),new Vector2(Screen.width,BottomPixels),new Vector2(Screen.width,Screen.height-TopPixels),new Vector2(0,Screen.height-TopPixels)};
            for(int c=0;c<4;c++)
            {
                var a=MapPoint(controller.CameraRig.Ground(corners[c]),r);
                var b=MapPoint(controller.CameraRig.Ground(corners[(c+1)%4]),r);
                // Keep clipping and rotation in the HUD's absolute logical space.
                // Rotating inside BeginGroup also moves its clip origin at high DPI.
                if(!RtsSkin.ClipLine(new Rect(r.x+.5f,r.y+.5f,r.width-1,r.height-1),ref a,ref b))continue;
                Matrix4x4 matrix=GUI.matrix;GUI.matrix=matrix*Matrix4x4.Translate(a)*Matrix4x4.Rotate(Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg))*Matrix4x4.Translate(-a);
                RtsSkin.Fill(new Rect(a.x,a.y-.5f,(b-a).magnitude,1),Color.white);GUI.matrix=matrix;
            }
        }
        static Vector2 MapPoint(Vector3 p,Rect r)=>new Vector2(r.x+(p.x-MapLayout.PlayableMin.x)/(MapLayout.PlayableMax.x-MapLayout.PlayableMin.x)*r.width,r.y+(MapLayout.PlayableMax.y-p.z)/(MapLayout.PlayableMax.y-MapLayout.PlayableMin.y)*r.height);
        static void Outline(Rect r,Color c) {RtsSkin.Fill(new Rect(r.x,r.y,r.width,1),c);RtsSkin.Fill(new Rect(r.x,r.yMax,r.width,1),c);RtsSkin.Fill(new Rect(r.x,r.y,1,r.height),c);RtsSkin.Fill(new Rect(r.xMax,r.y,1,r.height),c);}
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
            public readonly int[] PlayerCities = new int[PlayerRules.MaxPlayers];
            public readonly int[] PlayerMobile = new int[PlayerRules.MaxPlayers];
            public readonly int[] PlayerGuards = new int[PlayerRules.MaxPlayers];
            public readonly int[] PlayerUnits = new int[PlayerRules.MaxPlayers];
            public int RecruitmentReservations;
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
                return town.PotentialIncome;
            }
            public void Rebuild(BattleSession battle)
            {
                Gold = battle.Economy.Gold[0];
                Income = battle.Economy.Income(0);
                Population0 = battle.Population(0);
                Population1 = battle.Population(1);
                MobilePopulation0 = battle.RecruitmentPopulation(0);
                MobilePopulation1 = battle.RecruitmentPopulation(1);
                System.Array.Clear(PlayerCities,0,PlayerCities.Length);
                System.Array.Clear(PlayerMobile,0,PlayerMobile.Length);
                System.Array.Clear(PlayerGuards,0,PlayerGuards.Length);
                System.Array.Clear(PlayerUnits,0,PlayerUnits.Length);
                RecruitmentReservations=battle.RecruitmentReservations(0);
                foreach(var unit in battle.Units)
                    if(unit&&unit.IsAlive&&PlayerRules.IsPlayer(unit.Team))
                    {PlayerUnits[unit.Team]++;if(unit.IsGarrison)PlayerGuards[unit.Team]++;else PlayerMobile[unit.Team]++;}
                if(battle.Naval)foreach(var ship in battle.Naval.Ships)
                    if(ship&&ship.IsAlive&&PlayerRules.IsPlayer(ship.Team))PlayerUnits[ship.Team]++;
                MobilePopulation0=PlayerMobile[0];MobilePopulation1=PlayerMobile[1];
                GarrisonPopulation0=PlayerGuards[0];GarrisonPopulation1=PlayerGuards[1];
                OwnedTowns = 0;
                Round = battle.Economy.Round;
                RoundElapsed = battle.Economy.ElapsedInRound;
                for (int i = 0; i < Countries.Length; i++) Countries[i].Reset();
                foreach (Settlement town in battle.Towns)
                {
                    if (!town || town.State == null) continue;
                    if (town.State.Owner == 0) OwnedTowns++;
                    if(PlayerRules.IsPlayer(town.State.Owner))PlayerCities[town.State.Owner]++;
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
    }
}
