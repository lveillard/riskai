using System;
using System.Collections;
using System.IO;
using System.Linq;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskAI
{
    /// <summary>Opt-in desktop QA of the actual retained UI, with screenshots and resolved layout metrics.</summary>
    public sealed class RuntimeUiCapture : MonoBehaviour
    {
        string directory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnableWhenRequested()
        {
            var args=LaunchArguments.Get();
            for(int i=0;i<args.Length;i++)if(args[i]=="--riskai-restart-probe")return;
            for(int i=0;i<args.Length-1;i++) if(args[i]=="--riskai-ui-capture")
            {
                var host=new GameObject("RiskAI responsive UI capture");
                DontDestroyOnLoad(host);
                host.AddComponent<RuntimeUiCapture>().directory=args[i+1];
                break;
            }
        }

        IEnumerator Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Browser QA drives Review and captures the canvas through Playwright.
            // This opt-in route shares the actual UI and production commands.
            yield break;
#else
            Application.runInBackground=true;
            Directory.CreateDirectory(directory);
            yield return Settle();
            yield return Capture("front-end");
            if(UiViewport.IsCompact)
            {
                ScrollToEnd("Front end scroll");yield return Settle();yield return Capture("setup-fields");
            }
            var frontEnd=FindFirstObjectByType<FrontEndController>();
            if(!frontEnd){Debug.LogError("RISKAI_UI_CAPTURE_FAILED: requires front end");Application.Quit(1);yield break;}
            frontEnd.StartBattle();
            while(!BattleSession.Current)yield return null;
            var session=BattleSession.Current;
            session.AiEnabled=false;
            var controller=FindFirstObjectByType<RtsController>();
            controller.enabled=false;
            controller.FocusHome();
            Settlement selected=null;
            foreach(var town in session.Towns) if(town&&town.State.Owner==0&&!town.IsPort){selected=town;break;}
            if(selected)
            {
                controller.SelectTown(selected);
                session.Economy.Grant(0,1000);
                var commands=new PlayerBuildingCommands(session);
                foreach(var kind in new[]{UnitKind.Footman,UnitKind.Archer,UnitKind.Guard})
                    commands.Execute(0,PlayerBuildingIntent.Recruit(selected.BuildingId,kind));
            }
            yield return new WaitForSecondsRealtime(.12f);
            session.TogglePause();
            yield return Settle();
            yield return Capture("selection");
            yield return Settle();
            yield return Capture("production");
            if(UiViewport.IsCompact)
            {
                ScrollToEnd("HUD context");yield return Settle();yield return Capture("production-more");
            }
            foreach(var unit in session.Units)if(unit&&unit.Team==0){controller.SelectOnly(unit);controller.Focus(unit.transform.position);break;}
            yield return Settle();
            yield return Capture("orders");
            controller.HelpVisible=true;
            yield return Settle();
            yield return Capture("menu");
            ActivateButton("Ranking");
            yield return Settle();
            yield return Capture("ranking");
            controller.HelpVisible=false;
            foreach(var harbor in session.Naval.Harbors)
            {
                if(!harbor||harbor.Owner<0)continue;
                // A rendering fixture buys through that port's actual owner. It does
                // not grant the human controller authority over an enemy building.
                session.TogglePause();
                session.Economy.Grant(harbor.Owner,100);
                var commands=new PlayerBuildingCommands(session);
                commands.Execute(harbor.Owner,PlayerBuildingIntent.BuyShip(harbor.BuildingId,NavalUnitKind.Galley));
                commands.Execute(harbor.Owner,PlayerBuildingIntent.BuyShip(harbor.BuildingId,NavalUnitKind.Transport));
                session.TogglePause();
                controller.SelectHarbor(harbor);controller.Focus(harbor.Landing);
                yield return Settle();
                yield return Capture("port-queues");
                break;
            }
            Debug.Log("RISKAI_UI_CAPTURE_OK: "+Screen.width+"x"+Screen.height+" directory="+directory);
            Application.Quit();
#endif
        }

        public void Review(string stage)
        {
            if(string.IsNullOrEmpty(directory)||!BattleSession.Current)return;
            StartCoroutine(ReviewStage(stage));
        }

        IEnumerator ReviewStage(string stage)
        {
            var session=BattleSession.Current;var controller=FindFirstObjectByType<RtsController>();
            var hud=FindFirstObjectByType<BattleHud>();
            session.AiEnabled=false;controller.HelpVisible=false;
            if(stage=="empty")controller.Clear();
            else if(stage=="city"||stage=="queue")
            {
                var town=session.Towns.First(t=>t.State.Owner==0&&!t.IsPort);
                controller.SelectTown(town);controller.Focus(town.transform.position);
                if(stage=="queue")
                {
                    if(session.Paused)session.TogglePause();session.Economy.Grant(0,20);
                    foreach(var kind in new[]{UnitKind.Footman,UnitKind.Archer,UnitKind.Guard})
                        new PlayerBuildingCommands(session).Execute(0,PlayerBuildingIntent.Recruit(town.BuildingId,kind));
                }
            }
            else if(stage=="harbor")
            {
                var harbor=session.Naval.Harbors.FirstOrDefault(h=>h.Owner==0);
                if(!harbor){Debug.LogError("RISKAI_UI_REVIEW: no owned harbor in fixture seed");yield break;}
                if(session.Paused)session.TogglePause();session.Economy.Grant(0,30);
                var commands=new PlayerBuildingCommands(session);
                commands.Execute(0,PlayerBuildingIntent.BuyShip(harbor.BuildingId,NavalUnitKind.Galley));
                commands.Execute(0,PlayerBuildingIntent.BuyShip(harbor.BuildingId,NavalUnitKind.Transport));
                controller.SelectHarbor(harbor);controller.Focus(harbor.IsImportedPort?harbor.LinkedTown.transform.position:harbor.Landing);
            }
            else if(stage=="income")hud.ShowIncome();
            else if(stage=="ranking")hud.ShowPlayers();
            else if(stage=="strategic"){controller.Clear();controller.CameraRig.FrameMap();}
            else if(stage=="north"||stage=="south"||stage=="coast")
            {
                controller.Clear();
                float zoom=MapLayout.IsImported?85:62;
                for(int i=0;i<4;i++)controller.CameraRig.ZoomByRatio(controller.CameraRig.TargetZoom/zoom,UiViewport.WorldRect.center);
                var min=MapLayout.PlayableMin;var max=MapLayout.PlayableMax;
                var point=MapLayout.Point(Mathf.Lerp(min.x,max.x,.5f),Mathf.Lerp(min.y,max.y,stage=="north"?.76f:stage=="south"?.24f:.5f));
                if(stage=="coast")
                {
                    var harbor=session.Naval.Harbors.FirstOrDefault();
                    if(harbor)point=harbor.Berth;
                }
                controller.Focus(point);
            }
            if(!session.Paused)session.TogglePause();
            yield return new WaitForSecondsRealtime(1.5f);
            var elements=new System.Collections.Generic.List<ReviewElement>();
            foreach(var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                foreach(var element in document.rootVisualElement.Query<VisualElement>().ToList())
                    if((element is Button||element is Label||element.name=="HUD footer"||element.name=="HUD header")&&element.resolvedStyle.display!=DisplayStyle.None&&element.worldBound.width>0)
                        elements.Add(new ReviewElement { name=element.name,text=(element as TextElement)?.text,rect=element.worldBound,visible=element.visible });
            Debug.Log("RISKAI_UI_REVIEW "+JsonUtility.ToJson(new ReviewLayout { stage=stage,scale=UiViewport.Scale,world=UiViewport.WorldRect,elements=elements.ToArray() }));
        }

        [Serializable] sealed class ReviewLayout { public string stage;public float scale;public Rect world;public ReviewElement[] elements; }
        [Serializable] sealed class ReviewElement { public string name,text;public Rect rect;public bool visible; }

        static IEnumerator Settle()
        {
            yield return null;yield return null;
            yield return new WaitForSecondsRealtime(.5f);
        }

        static void ActivateButton(string label)
        {
            foreach(var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var buttons=document.rootVisualElement.Query<Button>().ToList();
                foreach(var button in buttons)
                    if(button.text==label||button.name==label)
                    {
                        button.Focus();
                        using(var evt=NavigationSubmitEvent.GetPooled()) { evt.target=button;button.SendEvent(evt); }
                        return;
                    }
            }
            Debug.LogError("RISKAI_UI_CAPTURE_FAILED: button not found "+label);
        }

        static void ScrollToEnd(string name)
        {
            foreach(var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var scroll=document.rootVisualElement.Q<ScrollView>(name);
                if(scroll!=null){scroll.verticalScroller.value=scroll.verticalScroller.highValue;return;}
            }
            Debug.LogError("RISKAI_UI_CAPTURE_FAILED: scroll not found "+name);
        }

        IEnumerator Capture(string stage)
        {
            var battle=BattleSession.Current;
            if(battle)Debug.Log("RISKAI_UI_MODEL: stage="+stage+" tick="+battle.Clock.TickCount+" gold="+battle.Economy.Gold[0]+" paused="+battle.Paused+" timeScale="+Time.timeScale+" realtime="+Time.unscaledTime);
            foreach(var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                foreach(var name in new[]{"HUD header","HUD footer","HUD context","HUD selection column","HUD contextual column","HUD unit portrait","HUD modal panel","HUD modal scroll","Front end content"})
                {
                    var element=document.rootVisualElement.Q<VisualElement>(name);
                    if(element==null)continue;
                    Debug.Log("RISKAI_UI_LAYOUT: stage="+stage+" element="+name+" rect="+element.worldBound+
                        " screen="+Screen.width+"x"+Screen.height+" scale="+UiViewport.Scale+" world="+UiViewport.WorldRect);
                }
            }
            string output=Path.Combine(directory,"v20-"+stage+".png");
            ScreenCapture.CaptureScreenshot(output);
            yield return new WaitForEndOfFrame();yield return null;
            float deadline=Time.unscaledTime+2;
            while(!File.Exists(output)&&Time.unscaledTime<deadline)yield return null;
            if(!File.Exists(output))
            {
                Debug.LogError("RISKAI_UI_CAPTURE_FAILED: no image written; capture requires a visible player window. "+output);
                Application.Quit(1);yield break;
            }
        }
    }
}
