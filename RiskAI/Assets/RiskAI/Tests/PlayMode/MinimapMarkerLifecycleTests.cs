using System.Collections;
using System.Reflection;
using NUnit.Framework;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RiskAI.Tests
{
    public sealed class MinimapMarkerLifecycleTests
    {
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly MethodInfo Refresh=typeof(BattleHud).GetMethod("RefreshMinimapMarkers",PrivateInstance);
        static readonly FieldInfo TextureField=typeof(BattleHud).GetField("minimapMarkers",PrivateInstance);
        Scene previous,scene;
        ScenarioMap previousMap;
        BattleSession.StartLayout previousLayout;
        BattleSession.VictoryMode previousMode;
        int previousPlayers,previousSeed;
        float previousTimeScale;
        BattleSession battle;
        BattleHud hud;
        Texture2D Texture=>(Texture2D)TextureField.GetValue(hud);
        void RefreshAt(Rect rect,float time)=>Refresh.Invoke(hud,new object[]{rect,time});

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previous=SceneManager.GetActiveScene();previousMap=BattleSession.MapForNewMatch;
            previousLayout=BattleSession.LayoutForNewMatch;previousMode=BattleSession.ModeForNewMatch;
            previousPlayers=BattleSession.PlayerCountForNewMatch;previousSeed=BattleSession.SeedForNewMatch;previousTimeScale=Time.timeScale;
            BattleSession.MapForNewMatch=ScenarioMap.Classic;BattleSession.LayoutForNewMatch=BattleSession.StartLayout.Fixed;
            BattleSession.ModeForNewMatch=BattleSession.VictoryMode.Conquest;BattleSession.PlayerCountForNewMatch=2;BattleSession.SeedForNewMatch=19031;
            scene=SceneManager.CreateScene("Minimap marker lifecycle");SceneManager.SetActiveScene(scene);
            new GameObject("Minimap marker bootstrap").AddComponent<RiskBootstrap>();
            battle=BattleSession.Current;battle.AiEnabled=false;
            Object.FindFirstObjectByType<RtsController>().enabled=false;
            hud=Object.FindFirstObjectByType<BattleHud>();hud.enabled=false;
            Assert.That(Refresh,Is.Not.Null);Assert.That(TextureField,Is.Not.Null);
            if(!battle.Paused)battle.TogglePause();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectionChangesRefreshAtTenHertzWithoutReplacingTexture()
        {
            var unit=BattleTestScenario.Mobile(battle,0,UnitKind.Footman,new Vector3(-30,0,-16));
            var rect=new Rect(31,47,512,384);
            unit.Select(true);RefreshAt(rect,1000f);
            var texture=Texture;var selectedPixels=texture.GetPixels32();
            AssertMarkerColor(texture,rect,unit.transform.position,(Color32)Color.white);
            unit.Select(false);RefreshAt(rect,1000.05f);
            Assert.That(Texture,Is.SameAs(texture),"Frames inside the interval reuse the GPU resource.");
            CollectionAssert.AreEqual(selectedPixels,texture.GetPixels32(),"Selection changes must wait until the next bounded upload.");
            RefreshAt(rect,1000.11f);
            Assert.That(Texture,Is.SameAs(texture),"A normal refresh reuses its texture.");
            AssertMarkerColor(texture,rect,unit.transform.position,(Color32)VisualFactory.TeamColor(unit.Team));
            CollectionAssert.AreNotEqual(selectedPixels,texture.GetPixels32(),"The next refresh must display the current selection color.");
            yield return null;
        }

        static void AssertMarkerColor(Texture2D texture,Rect rect,Vector3 world,Color32 expected)
        {
            float nx=(world.x-MapLayout.PlayableMin.x)/(MapLayout.PlayableMax.x-MapLayout.PlayableMin.x);
            float ny=(world.z-MapLayout.PlayableMin.y)/(MapLayout.PlayableMax.y-MapLayout.PlayableMin.y);
            int x=Mathf.Clamp(Mathf.FloorToInt(nx*texture.width),0,texture.width-1);
            int y=Mathf.Clamp(Mathf.FloorToInt(ny*texture.height),0,texture.height-1);
            Color32 actual=texture.GetPixel(x,y);
            Assert.That(actual.a,Is.GreaterThan(0),"The world point maps onto a visible marker pixel.");
            Assert.That(actual.r,Is.EqualTo(expected.r).Within(1));
            Assert.That(actual.g,Is.EqualTo(expected.g).Within(1));
            Assert.That(actual.b,Is.EqualTo(expected.b).Within(1));
        }

        [UnityTest]
        public IEnumerator ResizeRefreshesImmediatelyAndHudDestructionReleasesBothTextures()
        {
            RefreshAt(new Rect(0,0,120,90),2000f);
            var oldTexture=Texture;
            var resized=new Rect(19,23,256,192);
            RefreshAt(resized,2000.01f);
            var current=Texture;
            Assert.That(current,Is.Not.SameAs(oldTexture),"Resize rebuilds even inside the upload interval.");
            Assert.That(current.width,Is.EqualTo(Mathf.Clamp(Mathf.CeilToInt(resized.width*BattleHud.Scale),1,1024)));
            Assert.That(current.height,Is.EqualTo(Mathf.Clamp(Mathf.CeilToInt(resized.height*BattleHud.Scale),1,1024)));
            bool visible=false;foreach(var pixel in current.GetPixels32())if(pixel.a>0){visible=true;break;}
            Assert.That(visible,Is.True,"The resized overlay is populated immediately.");
            yield return null;
            Assert.That(oldTexture==null,Is.True,"The superseded texture must be destroyed after the frame.");
            Object.Destroy(hud);
            yield return null;yield return null;
            Assert.That(current==null,Is.True,"HUD teardown releases its last generated marker texture.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BattleSession.MapForNewMatch=previousMap;BattleSession.LayoutForNewMatch=previousLayout;BattleSession.ModeForNewMatch=previousMode;
            BattleSession.PlayerCountForNewMatch=previousPlayers;BattleSession.SeedForNewMatch=previousSeed;
            SceneManager.SetActiveScene(previous);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            MapLayout.Configure(previousMap);Time.timeScale=previousTimeScale;
        }
    }
}
