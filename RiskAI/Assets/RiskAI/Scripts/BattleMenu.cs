using UnityEngine;
using UnityEngine.SceneManagement;
namespace RiskAI
{
    public sealed partial class BattleHud
    {
        int menuTab;
        readonly int[] scoreOrder=new int[Core.PlayerRules.MaxPlayers];
        int campCityPage=-1,campCountry=-1;
        public void ShowPlayers(){menuTab=2;controller.HelpVisible=true;}
        void OpenInitialMenu() { }
        void Scores()
        {
            RtsSkin.Fill(new Rect(0,0,width,height),new Color(.015f,.025f,.032f,.62f));
            var r=new Rect(width/2-460,height/2-312,920,624);RtsSkin.Frame(r,RtsSkin.Gold);
            Label(r.x+30,r.y+28,850,"CLASIFICACIÓN · CIUDADES",RtsSkin.Title);
            Label(r.x+30,r.y+66,850,"Suelta Tab para volver a la partida.",RtsSkin.Small);
            PlayersPanel(r);
        }
        void Help()
        {
            RtsSkin.Fill(new Rect(0,0,width,height),new Color(.015f,.025f,.032f,.90f));
            var r=new Rect(width/2-460,height/2-330,920,660);RtsSkin.Frame(r,RtsSkin.Gold);
            Label(r.x+30,r.y+26,850,"DOMINIOS · "+MapLayout.MapName,RtsSkin.Title);
            menuTab=GUI.SelectionGrid(new Rect(r.x+30,r.y+79,860,42),menuTab,new[]{"PARTIDA","CONTROLES","RANKING","CÁMARA"},4,RtsSkin.Button);
            if(menuTab==0)
            {
                Label(r.x+35,r.y+164,830,"CONQUISTA EL 60 % DE LAS CIUDADES",RtsSkin.Title);
                Label(r.x+35,r.y+219,830,"Defiende los círculos. Completa países para recibir refuerzos.",RtsSkin.Small);
                Label(r.x+35,r.y+259,830,"Tus ciudades generan oro al terminar cada ronda.",RtsSkin.Small);
                Label(r.x+35,r.y+318,830,"Semilla "+session.Seed+"   ·   "+session.PlayerCount+" jugadores   ·   "+session.DifficultyName,RtsSkin.Small);
                Label(r.x+35,r.y+368,830,"La partida sigue en marcha salvo que la pauses con F10.",RtsSkin.Small);
                if(Button(new Rect(r.x+35,r.y+433,380,52),session.Paused?"CONTINUAR SIMULACIÓN":"PAUSAR SIMULACIÓN"))session.TogglePause();
                if(Button(new Rect(r.x+435,r.y+433,450,52),"NUEVA PARTIDA · ELEGIR MAPA"))FrontEndController.Open();
            }
            else if(menuTab==1)
            {
                string[] lines={"SELECCIÓN","Clic izquierdo o caja. Sin tropas dentro, la caja selecciona edificios.","Shift añade. Doble clic en tu ciudad agrupa tus ciudades cercanas.","ÓRDENES","Clic derecho mueve o ataca. A + clic avanza combatiendo. S detiene. H mantiene.","Un defensor puede salir cuando tiene un relevo aliado dentro de su círculo.","CÁMARA Y MAPA","Arrastra con botón derecho o central. Rueda: zoom bajo el cursor.","Bordes o flechas: desplazar. F2: ciudad inicial. F3: puerto. Tab: ranking.","Ciudad / puerto: recluta. Hoguera: país y refuerzos. Clic derecho fija su salida.","Ctrl + 1…9 guarda grupos. B/D: embarcar/desembarcar. Esc libera el cursor."};
                for(int i=0;i<lines.Length;i++)Label(r.x+32,r.y+150+i*35,850,lines[i],RtsSkin.Small);
            }
            else if(menuTab==2)PlayersPanel(r);
            else
            {
                Label(r.x+35,r.y+175,820,"DESPLAZAMIENTO",RtsSkin.Title);
                Label(r.x+35,r.y+236,250,"Velocidad de cámara",RtsSkin.Small);
                controller.CameraRig.PanSpeed=GUI.HorizontalSlider(new Rect(r.x+310,r.y+242,470,30),controller.CameraRig.PanSpeed,.5f,2.2f);
                controller.EdgePan=GUI.Toggle(new Rect(r.x+35,r.y+300,800,40),controller.EdgePan,"Desplazar al acercarse al borde de la ventana",RtsSkin.Button);
                Label(r.x+35,r.y+379,820,"El zoom lejano cambia a una vista ligera de territorios.",RtsSkin.Small);
                Label(r.x+35,r.y+414,820,"Ángulo, sensibilidad y umbral estratégico son comunes a todos los mapas.",RtsSkin.Small);
                if(Button(new Rect(r.x+35,r.y+477,350,48),"RESTABLECER VISTA"))controller.CameraRig.ResetView();
            }
            if(Button(new Rect(r.x+30,r.y+591,860,45),"VOLVER A LA PARTIDA"))controller.HelpVisible=false;
        }
        void PlayersPanel(Rect r)
        {
            for(int i=0;i<session.PlayerCount;i++)scoreOrder[i]=i;
            // Stable insertion sort: leading city count first, player ID breaks ties.
            for(int i=1;i<session.PlayerCount;i++)
            {int item=scoreOrder[i],j=i-1;while(j>=0&&hud.PlayerCities[scoreOrder[j]]<hud.PlayerCities[item]){scoreOrder[j+1]=scoreOrder[j];j--;}scoreOrder[j+1]=item;}
            Label(r.x+30,r.y+144,400,"JUGADOR",RtsSkin.Tiny);
            Label(r.x+445,r.y+144,120,"CIUDADES",RtsSkin.Tiny);
            Label(r.x+570,r.y+144,110,"ORO",RtsSkin.Tiny);
            Label(r.x+690,r.y+144,180,"TROPAS / GUARDIAS",RtsSkin.Tiny);
            for(int rank=0;rank<session.PlayerCount;rank++)
            {
                int player=scoreOrder[rank];float y=r.y+178+rank*25;
                if(player==0)RtsSkin.Fill(new Rect(r.x+24,y-1,872,25),new Color(.16f,.23f,.3f,.75f));
                Label(r.x+32,y,38,(rank+1).ToString(),RtsSkin.Small);
                RtsSkin.Fill(new Rect(r.x+73,y+5,12,12),VisualFactory.TeamColor(player));
                Label(r.x+100,y,330,VisualFactory.TeamName(player),RtsSkin.Small);
                Label(r.x+465,y,90,hud.PlayerCities[player].ToString(),RtsSkin.Small);
                Label(r.x+580,y,100,session.Economy.Gold[player].ToString(),RtsSkin.Small);
                Label(r.x+715,y,160,hud.PlayerMobile[player]+" / "+hud.PlayerGuards[player],RtsSkin.Small);
            }
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
