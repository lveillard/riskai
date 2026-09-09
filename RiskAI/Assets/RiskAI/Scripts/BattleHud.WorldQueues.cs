using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskAI
{
    public sealed partial class BattleHud
    {
        VisualElement worldQueues;
        readonly Dictionary<int,WorldQueue> worldQueueViews=new Dictionary<int,WorldQueue>();
        readonly List<int> expiredQueues=new List<int>();

        void BuildWorldQueues(VisualElement root)
        {
            worldQueueViews.Clear();
            worldQueues=new VisualElement { name="HUD world queues",pickingMode=PickingMode.Ignore };
            worldQueues.style.position=Position.Absolute;worldQueues.style.left=worldQueues.style.top=worldQueues.style.right=worldQueues.style.bottom=0;
            root.Add(worldQueues);
        }

        void RefreshWorldQueues()
        {
            if(worldQueues==null)return;
            bool visible=ModalKind==0&&!session.IsStarting&&!StrategicMapView.Active;
            worldQueues.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
            // Ownership/lifetime is still reconciled while a modal hides the layer.
            foreach(var town in session.Towns)
                if(town&&!(town.IsPort&&town.Port)&&town.State.Owner==0&&town.QueueCount>0)
                    EnsureWorldQueue(town.GetInstanceID(),town,null);
            if(session.Naval)foreach(var harbor in session.Naval.Harbors)
                if(harbor&&harbor.Owner==0&&harbor.QueueCount+harbor.LandQueueCount>0)
                    EnsureWorldQueue(harbor.GetInstanceID(),null,harbor);
            expiredQueues.Clear();
            foreach(var pair in worldQueueViews)
            {
                var view=pair.Value;
                if(!view.Owned||view.Count==0) { view.Root.RemoveFromHierarchy();expiredQueues.Add(pair.Key);continue; }
                if(visible)view.Refresh(this);
            }
            foreach(int key in expiredQueues)worldQueueViews.Remove(key);
        }

        void EnsureWorldQueue(int id,Settlement town,Harbor harbor)
        {
            if(worldQueueViews.ContainsKey(id))return;
            var view=new WorldQueue(town,harbor,id);worldQueueViews.Add(id,view);worldQueues.Add(view.Root);
            view.AddRow(this,false);if(harbor)view.AddRow(this,true);
        }

        sealed class WorldQueue
        {
            readonly Settlement town;
            readonly Harbor harbor;
            readonly List<WorldQueueSlot> slots=new List<WorldQueueSlot>(10);
            VisualElement landRow,navalRow;
            public readonly VisualElement Root;
            public bool Owned=>town?town.State.Owner==0:harbor&&harbor.Owner==0;
            int LandCount=>town?town.QueueCount:harbor?harbor.LandQueueCount:0;
            int NavalCount=>harbor?harbor.QueueCount:0;
            public int Count=>LandCount+NavalCount;
            public WorldQueue(Settlement town,Harbor harbor,int id)
            {
                this.town=town;this.harbor=harbor;
                Root=new VisualElement { name="HUD building queue "+id,pickingMode=PickingMode.Ignore };
                Root.style.position=Position.Absolute;Root.style.alignItems=Align.Center;
            }

            public void AddRow(BattleHud hud,bool naval)
            {
                var row=new VisualElement { pickingMode=PickingMode.Ignore };RtsUiStyle.Row(row);Root.Add(row);
                if(naval)navalRow=row;else landRow=row;
                for(int i=0;i<Harbor.QueueCapacity;i++)
                {
                    int index=i;
                    var button=RtsUiStyle.Button("",()=>
                    {
                        if(!Owned)return;
                        if(town)hud.controller.CancelTraining(town,index);else hud.controller.CancelTraining(harbor,index,naval);
                    },"HUD queue item "+(naval?"sea ":"land ")+i);
                    button.style.width=button.style.minWidth=44;button.style.height=button.style.minHeight=46;
                    button.style.marginLeft=button.style.marginTop=button.style.marginBottom=0;button.style.marginRight=2;
                    button.style.paddingLeft=button.style.paddingRight=button.style.paddingTop=button.style.paddingBottom=3;
                    button.style.alignItems=Align.Center;button.style.flexShrink=0;
                    var frame=PortraitFrame(PortraitResource(UnitKind.Archer),32);button.Add(frame);
                    var track=new VisualElement { pickingMode=PickingMode.Ignore };track.style.width=32;track.style.height=4;track.style.marginTop=2;track.style.backgroundColor=RtsUiStyle.Slate;
                    var progress=new VisualElement { pickingMode=PickingMode.Ignore };progress.style.height=4;progress.style.backgroundColor=RtsUiStyle.Gold;track.Add(progress);button.Add(track);
                    row.Add(button);slots.Add(new WorldQueueSlot { Naval=naval,Index=i,Button=button,Portrait=frame.Q<Image>(),Progress=progress });
                }
            }

            public void Refresh(BattleHud hud)
            {
                int land=LandCount,naval=NavalCount;
                landRow.style.display=land>0?DisplayStyle.Flex:DisplayStyle.None;
                if(navalRow!=null)navalRow.style.display=naval>0?DisplayStyle.Flex:DisplayStyle.None;
                float width=Mathf.Max(land,naval)*46,height=(land>0?46:0)+(naval>0?46:0);
                var anchor=town?town.transform.position:harbor.IsImportedPort?harbor.LinkedTown.transform.position:harbor.Landing;
                Vector3 point=hud.cam.WorldToScreenPoint(anchor+Vector3.up*6.8f);
                Rect world=UiViewport.WorldRect;
                float x=point.x-width*UiViewport.Scale*.5f,y=point.y+8*UiViewport.Scale;
                var screen=new Rect(x,y,width*UiViewport.Scale,height*UiViewport.Scale);
                bool onScreen=point.z>0&&screen.xMin>=world.xMin&&screen.xMax<=world.xMax&&screen.yMin>=world.yMin&&screen.yMax<=world.yMax;
                Root.style.display=onScreen?DisplayStyle.Flex:DisplayStyle.None;
                if(!onScreen)return;
                Root.style.width=width;Root.style.height=height;
                Root.style.left=(x-UiViewport.SafeRect.xMin)/UiViewport.Scale;
                Root.style.top=(UiViewport.SafeRect.yMax-screen.yMax)/UiViewport.Scale;
                foreach(var slot in slots)
                {
                    bool active=slot.Index<(slot.Naval?naval:land);
                    slot.Button.style.display=active?DisplayStyle.Flex:DisplayStyle.None;
                    if(!active)continue;
                    string resource,name;float progress;
                    if(slot.Naval)
                    {
                        var kind=harbor.QueuedKind(slot.Index);resource=ShipPortrait.Resource(kind);name=Harbor.Profile(kind).Name;progress=harbor.TrainingProgress;
                    }
                    else
                    {
                        var kind=town?town.QueuedKind(slot.Index):harbor.QueuedLandKind(slot.Index);
                        resource=PortraitResource(kind);name=BattleRules.Name(kind);progress=town?town.TrainingProgress:harbor.LandTrainingProgress;
                    }
                    var texture=CachedPortrait(resource);if(slot.Portrait.image!=texture)slot.Portrait.image=texture;
                    slot.Progress.style.width=Length.Percent(slot.Index==0?Mathf.Clamp01(progress)*100:0);
                    slot.Button.tooltip=name+" · cancelar encargo";
                }
            }
        }

        sealed class WorldQueueSlot
        {
            public bool Naval;public int Index;public Button Button;public Image Portrait;public VisualElement Progress;
        }
    }
}
