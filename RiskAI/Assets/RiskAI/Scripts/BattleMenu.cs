using UnityEngine;
using UnityEngine.SceneManagement;
namespace RiskAI
{
    public sealed partial class BattleHud
    {
        static bool firstLaunch=true;
        int menuTab;
        bool initialMenu;
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
            menuTab=GUI.SelectionGrid(new Rect(r.x+30,r.y+102,860,38),menuTab,new[]{"PARTIDA","CONTROLES","AJUSTES"},3,RtsSkin.Button);
            if(menuTab==0)
            {
                MapCard(new Rect(r.x+30,r.y+160,418,151),false,"LAS MARCAS","12 ciudades · 6 grupos","Costas, dos mesetas y expediciones a las islas. Escenario compacto para aprender y comparar.");
                MapCard(new Rect(r.x+472,r.y+160,418,151),true,"CUATRO RIBERAS","20 ciudades · 4 dominios + archipiélago","Río central, puente y más tierras al norte. Grupos amplios y rutas marítimas alternativas.");
                Label(r.x+30,r.y+333,210,"REPARTO INICIAL",RtsSkin.Small);
                BattleSession.LayoutForNewMatch=(BattleSession.StartLayout)GUI.SelectionGrid(new Rect(r.x+255,r.y+327,635,37),(int)BattleSession.LayoutForNewMatch,new[]{"Ciudades al azar","Grupos iniciales","Posiciones fijas"},3,RtsSkin.Button);
                Label(r.x+30,r.y+388,200,"SEMILLA",RtsSkin.Small);
                seedText=GUI.TextField(new Rect(r.x+255,r.y+382,280,35),seedText,11);
                if(Button(new Rect(r.x+551,r.y+382,150,35),"Otra semilla")){BattleSession.NewSeed();seedText=BattleSession.SeedForNewMatch.ToString();}
                Label(r.x+30,r.y+443,820,"CONQUISTA · controla el 60 % de las ciudades.",RtsSkin.Small);
                Label(r.x+30,r.y+477,820,"Empiezas con 4 de oro y un defensor por puesto. Compra tu primera tropa en una ciudad.",RtsSkin.Small);
                Label(r.x+30,r.y+511,820,"Pulsa una hoguera para ver su grupo y dónde aparecen sus refuerzos.",RtsSkin.Small);
                if(Button(new Rect(r.x+590,r.y+595,300,54),"EMPEZAR PARTIDA"))StartMatch();
            }
            else if(menuTab==1)
            {
                string[] lines={"SELECCIÓN","Clic izquierdo o caja · Shift añade · doble clic elige el mismo tipo.","ÓRDENES","Clic derecho: mover, atacar o seguir. A + clic: avanzar atacando. S: detener. H: mantener.","CÁMARA","Arrastra con botón derecho o central. Arrastrar nunca da una orden al soltar.","Rueda: zoom bajo el cursor · bordes o flechas: desplazar · Espacio: centrar.","ECONOMÍA Y MAPA","Ciudad: soldados · puerto: flota · hoguera: grupo, ciudades y refuerzos.","F2: base inicial · F3: puerto · N: flota · B/D: embarcar/desembarcar.","Ctrl + 1…9 guarda grupos · 1…9 recupera · F10 pausa · Esc libera el cursor."};
                for(int i=0;i<lines.Length;i++)Label(r.x+32,r.y+167+i*34,855,lines[i],RtsSkin.Small);
            }
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
            Label(r.x+30,r.y+656,820,"Escenario actual: "+MapLayout.MapName+" · semilla "+session.Seed,RtsSkin.Tiny);
        }
        void MapCard(Rect r,bool expanded,string name,string stats,string description)
        {
            bool selected=BattleSession.ExpandedMapForNewMatch==expanded;
            RtsSkin.Frame(r,selected?RtsSkin.Gold:new Color(.26f,.3f,.29f));
            if(Button(new Rect(r.x+12,r.y+12,r.width-24,39),(selected?"● ":"")+name))BattleSession.ExpandedMapForNewMatch=expanded;
            Label(r.x+18,r.y+60,r.width-36,stats,RtsSkin.Small);
            Text(new Rect(r.x+18,r.y+88,r.width-36,53),description,new GUIStyle(RtsSkin.Small){wordWrap=true});
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
            Label(253,bottom+18,650,camp.DisplayName.ToUpperInvariant(),RtsSkin.Title);
            Label(253,bottom+52,630,"HOGUERA · "+group.Owned+" / "+group.CityCount+" ciudades",RtsSkin.Small);
            Label(253,bottom+85,630,group.Owner<0?"Completa el grupo para activar ingresos y refuerzos.":"Grupo controlado por "+(group.Owner==0?"tu ejército.":"el enemigo."),RtsSkin.Small);
            Label(253,bottom+116,630,"Cada ronda: "+rule.PerTurn+" × "+Core.BattleRules.Name(rule.Reinforcement)+" en esta hoguera.",RtsSkin.Small);
            Label(253,bottom+147,630,"La superposición muestra el grupo; los anillos señalan sus ciudades.",RtsSkin.Tiny);
            Label(x,bottom+17,590,"CIUDADES DEL GRUPO",RtsSkin.Title);
            for(int i=0;i<group.CityCount;i++)
            {
                var town=group.Cities[i];int column=i%2,row=i/2;
                if(Button(new Rect(x+column*260,bottom+52+row*36,250,30),town.DisplayName+(town.State.Owner==0?" · tuya":town.State.Owner==1?" · rival":" · libre")))
                {controller.Focus(town.transform.position);controller.SelectTown(town);}
            }
        }
    }
}
