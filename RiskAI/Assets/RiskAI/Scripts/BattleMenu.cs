using UnityEngine;
using UnityEngine.SceneManagement;
namespace RiskAI
{
    public sealed partial class BattleHud
    {
        static bool firstLaunch=true;
        int menuTab;
        public void ShowPlayers() { menuTab=2; controller.HelpVisible=true; }
        void Scores()
        {
            RtsSkin.Fill(new Rect(0,0,width,height),new Color(.015f,.025f,.032f,.62f));
            var r=new Rect(width/2-460,height/2-312,920,624);RtsSkin.Frame(r,RtsSkin.Gold);
            Label(r.x+30,r.y+28,850,"MARCADORES · TAB",RtsSkin.Title);
            Label(r.x+30,r.y+74,850,"Suelta Tab para volver.",RtsSkin.Small);
            PlayersPanel(r);
        }
        bool initialMenu;
        int campCityPage=-1, campCountry=-1;
        void OpenInitialMenu()
        {
            if(!firstLaunch||Application.isEditor||Application.isBatchMode||System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--riskai-capture")>=0)return;
            firstLaunch=false;initialMenu=true;controller.HelpVisible=true;
            if(!session.Paused)session.TogglePause();
        }
        void Help()
        {
            RtsSkin.Fill(new Rect(0,0,width,height),new Color(.015f,.025f,.032f,.88f));
            var r=new Rect(width/2-460,height/2-342,920,684);RtsSkin.Frame(r,RtsSkin.Gold);
            Label(r.x+30,r.y+24,820,"RISKAI · DOMINIOS",RtsSkin.Title);
            Label(r.x+30,r.y+62,820,"Elige tu escenario. Protege sus defensores y reúne los grupos.",RtsSkin.Small);
            menuTab=GUI.SelectionGrid(new Rect(r.x+30,r.y+102,860,38),menuTab,new[]{"PARTIDA","CONTROLES","JUGADORES","AJUSTES"},4,RtsSkin.Button);
            if(menuTab==0)
            {
                MapCard(new Rect(r.x+30,r.y+155,418,96),ScenarioMap.Classic,"LAS MARCAS","18 ciudades · 9 grupos","Costas, dos mesetas y nuevas marcas secas del sur.");
                MapCard(new Rect(r.x+472,r.y+155,418,96),ScenarioMap.Riverlands,"CUATRO RIBERAS","20 ciudades · 5 grupos","Río central, puente y archipiélago del norte.");
                MapCard(new Rect(r.x+30,r.y+259,418,96),ScenarioMap.Europe,"Europe · Reforged","212 ciudades · 69 grupos · 44 puertos","Territorio europeo importado y reinterpretado para Dominios.");
                MapCard(new Rect(r.x+472,r.y+259,418,96),ScenarioMap.NewWorld,"New World · Europa y América","293 ciudades · 100 grupos · 59 puertos","Europa y América en un escenario de gran escala.");
                Label(r.x+30,r.y+372,210,"JUGADORES",RtsSkin.Small);
                if(Button(new Rect(r.x+255,r.y+366,38,34),"−"))BattleSession.PlayerCountForNewMatch=Mathf.Max(2,BattleSession.PlayerCountForNewMatch-1);
                Label(r.x+315,r.y+372,430,BattleSession.PlayerCountForNewMatch+" · tú y "+(BattleSession.PlayerCountForNewMatch-1)+" IA independientes",RtsSkin.Small);
                if(Button(new Rect(r.x+800,r.y+366,38,34),"+"))BattleSession.PlayerCountForNewMatch=Mathf.Min(Core.PlayerRules.MaxPlayers,BattleSession.PlayerCountForNewMatch+1);
                Label(r.x+30,r.y+415,210,"REPARTO INICIAL",RtsSkin.Small);
                BattleSession.LayoutForNewMatch=(BattleSession.StartLayout)GUI.SelectionGrid(new Rect(r.x+255,r.y+408,635,34),(int)BattleSession.LayoutForNewMatch,new[]{"Ciudades al azar","Grupos iniciales",BattleSession.PlayerCountForNewMatch > 2 ? "Ciudades por semilla" : "Posiciones fijas"},3,RtsSkin.Button);
                Label(r.x+30,r.y+458,200,"SEMILLA",RtsSkin.Small);
                seedText=GUI.TextField(new Rect(r.x+255,r.y+452,280,34),seedText,11);
                if(Button(new Rect(r.x+551,r.y+452,150,34),"Otra semilla")){BattleSession.NewSeed();seedText=BattleSession.SeedForNewMatch.ToString();}
                ImportedLandscapeAugment.Enabled=GUI.Toggle(new Rect(r.x+30,r.y+496,850,30),ImportedLandscapeAugment.Enabled,"Añadir cordilleras en Europe / New World");
                Label(r.x+30,r.y+534,820,"Todos empiezan con 4 de oro y un defensor por puesto. No hay ejércitos gratuitos.",RtsSkin.Small);
                Label(r.x+30,r.y+561,820,"CONQUISTA · controla el 60 % de las ciudades. Las distancias conservan la escala original.",RtsSkin.Tiny);
                if(Button(new Rect(r.x+590,r.y+595,300,54),"EMPEZAR PARTIDA"))StartMatch();
            }
            else if(menuTab==1)
            {
                string[] lines={"SELECCIÓN","Clic izquierdo o caja · Shift añade · doble clic elige el mismo tipo.","ÓRDENES","Clic derecho: mover, atacar o seguir. A + clic: avanzar atacando. S: detener. H: mantener.","CÁMARA","Arrastra con botón derecho o central. Arrastrar nunca da una orden al soltar.","Rueda: zoom bajo el cursor · bordes o flechas: desplazar · Espacio: centrar.","ECONOMÍA Y MAPA","Ciudad: soldados · puerto: flota · hoguera: grupo, ciudades y refuerzos.","F2: base inicial · F3: puerto · N: flota · B/D: embarcar/desembarcar.","Ctrl + 1…9 guarda grupos · 1…9 recupera · F10 pausa · Esc libera el cursor."};
                for(int i=0;i<lines.Length;i++)Label(r.x+32,r.y+167+i*34,855,lines[i],RtsSkin.Small);
            }
            else if(menuTab==2)PlayersPanel(r);
            else
            {
                Label(r.x+30,r.y+180,820,"CÁMARA",RtsSkin.Title);
                Label(r.x+30,r.y+233,260,"Velocidad de desplazamiento",RtsSkin.Small);
                controller.CameraRig.PanSpeed=GUI.HorizontalSlider(new Rect(r.x+320,r.y+238,430,25),controller.CameraRig.PanSpeed,.5f,2.2f);
                controller.EdgePan=GUI.Toggle(new Rect(r.x+30,r.y+285,700,35),controller.EdgePan,"Desplazar al acercarse al borde de la ventana");
                Label(r.x+30,r.y+356,820,"IA DE LA SIGUIENTE PARTIDA",RtsSkin.Title);
                BattleSession.DifficultyForNewMatch=(BattleSession.AiDifficulty)GUI.SelectionGrid(new Rect(r.x+30,r.y+406,860,46),(int)BattleSession.DifficultyForNewMatch,new[]{"Relajada · ataque más tarde","Estándar · presión temprana"},2,RtsSkin.Button);
                Label(r.x+30,r.y+478,820,"Ambas reaccionan para defender sus ciudades desde el comienzo.",RtsSkin.Small);
            }
            if(!initialMenu&&Button(new Rect(r.x+30,r.y+595,250,54),"VOLVER A LA PARTIDA"))controller.HelpVisible=false;
            Label(r.x+30,r.y+656,820,"Próximo escenario: "+ScenarioName(BattleSession.MapForNewMatch)+" · semilla "+BattleSession.SeedForNewMatch,RtsSkin.Tiny);
        }
        void MapCard(Rect r,ScenarioMap scenario,string name,string stats,string description)
        {
            bool selected=BattleSession.MapForNewMatch==scenario;
            RtsSkin.Frame(r,selected?RtsSkin.Gold:new Color(.26f,.3f,.29f));
            if(Button(new Rect(r.x+12,r.y+10,r.width-24,32),(selected?"● ":"")+name))BattleSession.MapForNewMatch=scenario;
            Label(r.x+18,r.y+48,r.width-36,stats,RtsSkin.Tiny);
            Text(new Rect(r.x+18,r.y+66,r.width-36,25),description,new GUIStyle(RtsSkin.Tiny){wordWrap=true});
        }
        static string ScenarioName(ScenarioMap scenario)=>scenario==ScenarioMap.Riverlands?"Cuatro Riberas":scenario==ScenarioMap.Europe?"Europe · Reforged":scenario==ScenarioMap.NewWorld?"New World · Europa y América":"Las Marcas";
        void PlayersPanel(Rect r)
        {
            Label(r.x+30,r.y+163,850,"PARTIDA ACTUAL · "+session.PlayerCount+" jugadores · todos contra todos",RtsSkin.Title);
            for(int player=0;player<session.PlayerCount;player++)
            {
                float x=r.x+30+(player/8)*445,y=r.y+210+(player%8)*43;
                RtsSkin.Fill(new Rect(x,y+5,15,15),VisualFactory.TeamColor(player));
                Label(x+25,y,385,VisualFactory.TeamName(player)+" · "+hud.PlayerCities[player]+" ciudades",RtsSkin.Small);
                Label(x+25,y+19,385,session.Economy.Gold[player]+" oro · "+hud.PlayerMobile[player]+" móviles · "+hud.PlayerGuards[player]+" guardias · "+session.Kills[player]+" bajas",RtsSkin.Tiny);
            }
        }
        void StartMatch()
        {
            if(!int.TryParse(seedText,out int seed)){session.Message("Escribe una semilla numérica válida.");return;}
            BattleSession.SeedForNewMatch=seed;BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        void CampDetails(CountryCamp camp,float x)
        {
            var group=hud.Countries[camp.Country];var rule=MapLayout.Countries[camp.Country];
            if(campCountry!=camp.Country){campCountry=camp.Country;campCityPage=0;}
            Label(253,bottom+18,650,camp.DisplayName.ToUpperInvariant(),RtsSkin.Title);
            Label(253,bottom+52,630,"HOGUERA · "+group.Owned+" / "+group.CityCount+" ciudades",RtsSkin.Small);
            Label(253,bottom+85,630,group.Owner<0?"Completa el grupo para activar sus refuerzos.":"Grupo controlado por "+VisualFactory.TeamName(group.Owner),RtsSkin.Small);
            Label(253,bottom+116,630,"Cada ronda: "+rule.PerTurn+" × "+Core.BattleRules.Name(rule.Reinforcement)+" en esta hoguera.",RtsSkin.Small);
            Label(253,bottom+147,630,camp.HasRally?"Salida fijada · clic derecho cambia el punto de reunión.":"Sin salida: los refuerzos esperan aquí. Clic derecho fija su destino.",RtsSkin.Tiny);
            if(camp.HasRally && group.Owner==0 && Button(new Rect(253,bottom+174,280,27),"Quitar salida · esperar aquí"))camp.ClearRally();
            Label(x,bottom+17,590,"CIUDADES DEL GRUPO",RtsSkin.Title);
            const int perPage=6;int pages=Mathf.Max(1,Mathf.CeilToInt(group.CityCount/(float)perPage));campCityPage=Mathf.Clamp(campCityPage,0,pages-1);
            int start=campCityPage*perPage,end=Mathf.Min(group.CityCount,start+perPage);
            Label(x+292,bottom+20,105,(start+1)+"–"+end+" / "+group.CityCount,RtsSkin.Tiny);
            bool enabled=GUI.enabled;GUI.enabled=campCityPage>0;if(Button(new Rect(x+404,bottom+15,34,27),"‹"))campCityPage--;GUI.enabled=enabled;
            enabled=GUI.enabled;GUI.enabled=campCityPage<pages-1;if(Button(new Rect(x+444,bottom+15,34,27),"›"))campCityPage++;GUI.enabled=enabled;
            for(int i=start;i<end;i++)
            {
                var town=group.Cities[i];int relative=i-start,column=relative%2,row=relative/2;
                if(Button(new Rect(x+column*260,bottom+52+row*36,250,30),town.DisplayName+(town.State.Owner==0?" · tuya":town.State.Owner>=0?" · IA "+town.State.Owner:" · libre")))
                {controller.Focus(town.transform.position);controller.SelectTown(town);}
            }
        }
    }
}
