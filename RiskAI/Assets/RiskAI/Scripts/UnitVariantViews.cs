using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>
    /// Original presentation for the v0.30 source roster, built from the existing KayKit
    /// prefabs and procedural parts. Silhouettes are scaled roughly by source collision.
    /// </summary>
    public static class UnitVariantViews
    {
        static readonly Dictionary<string,string> resolvedPortraits = new Dictionary<string,string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => resolvedPortraits.Clear();

        /// <summary>Kinds whose portrait is rendered from a variant rather than a shared model prefab.</summary>
        public static readonly UnitKind[] PortraitKinds = { UnitKind.EliteRifleman, UnitKind.Roarer, UnitKind.ArmyGeneral, UnitKind.Artillery, UnitKind.Tank };

        public static string PortraitName(UnitKind kind)
        {
            switch(kind)
            {
                case UnitKind.Guard: return "MountedKnight";
                case UnitKind.EliteRifleman: return "EliteRifleman";
                case UnitKind.Roarer: return "Roarer";
                case UnitKind.ArmyGeneral: return "ArmyGeneral";
                case UnitKind.Artillery: return "Artillery";
                case UnitKind.Tank: return "Tank";
                default: return BattleRules.Model(kind);
            }
        }

        static string FallbackPortrait(UnitKind kind)
        {
            switch(kind)
            {
                case UnitKind.ArmyGeneral: return "MountedKnight";
                case UnitKind.Tank: return "Mortar";
                default: return BattleRules.Model(kind);
            }
        }

        /// <summary>Resources path of the unit portrait; falls back to the base model until the art setup renders it.</summary>
        public static string PortraitResource(UnitKind kind) => Resolve(PortraitName(kind),FallbackPortrait(kind));
        public static string PortraitResource(ShipKind kind) =>
            Resolve(kind.ToString(),kind==ShipKind.ArmoredTransport?ShipKind.Transport.ToString():kind==ShipKind.Transport?kind.ToString():ShipKind.Galley.ToString());

        static string Resolve(string preferred,string fallback)
        {
            if(resolvedPortraits.TryGetValue(preferred,out var path))return path;
            path="Portraits/"+preferred;
            if(preferred!=fallback&&!Resources.Load<Texture2D>(path))path="Portraits/"+fallback;
            resolvedPortraits[preferred]=path;return path;
        }

        /// <summary>Builds fully procedural variants; returns false when the prefab path should be used.</summary>
        public static bool TryCreate(Soldier soldier,Color team)
        {
            var root=soldier.transform;
            switch(soldier.Kind)
            {
                case UnitKind.ArmyGeneral:
                {
                    General(root,soldier.Team,soldier);
                    VisualFactory.Ring(root,.82f,.025f,team);return true;
                }
                case UnitKind.Artillery:
                {
                    var model=new GameObject("Artillery model");model.transform.SetParent(root,false);
                    var barrel=ArtilleryModel(model.transform,team);ModelMetrics.MatchStandingHeight(model,soldier.Kind);
                    SiegeUnitView.Attach(soldier,barrel);VisualFactory.Ring(root,.96f,.025f,team);return true;
                }
                case UnitKind.Tank:
                {
                    var model=new GameObject("Tank model");model.transform.SetParent(root,false);
                    var barrel=TankModel(model.transform,team);ModelMetrics.MatchStandingHeight(model,soldier.Kind);
                    SiegeUnitView.Attach(soldier,barrel);VisualFactory.Ring(root,.92f,.025f,team);return true;
                }
                default: return false;
            }
        }

        /// <summary>Adds identity parts to a prefab-based variant after its height and team colour are applied.</summary>
        public static void Decorate(GameObject model,UnitKind kind,int team)
        {
            if(!model)return;
            if(kind==UnitKind.EliteRifleman)EliteRifleman(model,team);
            else if(kind==UnitKind.Roarer)Roarer(model,team);
        }

        static readonly Color Gold=new Color(.80f,.60f,.22f),Steel=new Color(.52f,.57f,.62f),DarkWood=new Color(.24f,.13f,.06f),Iron=new Color(.20f,.22f,.24f),Fur=new Color(.34f,.24f,.15f);

        static void EliteRifleman(GameObject model,int team)
        {
            if(model.transform.Find("Elite rifleman identity"))return;
            foreach(var part in model.GetComponentsInChildren<Transform>(true))
                if(part.name=="1H_Crossbow"||part.name=="2H_Crossbow"||part.name=="Knife_Offhand")part.gameObject.SetActive(false);
            var identity=new GameObject("Elite rifleman identity").transform;identity.SetParent(model.transform,false);
            Color cloth=VisualFactory.TeamMaterialColor(team);
            Transform head=Find(model.transform,"head")??identity;
            // Tall team plume and gold band: reads as veteran infantry at game distance.
            VisualFactory.Shape(head,PrimitiveType.Cylinder,"Elite gold hat band",new Vector3(0,.36f,0),new Vector3(.52f,.05f,.5f),Gold);
            var plume=VisualFactory.Shape(head,PrimitiveType.Capsule,"Elite team plume",new Vector3(.12f,.62f,-.05f),new Vector3(.14f,.28f,.14f),cloth);
            plume.transform.localRotation=Quaternion.Euler(-12,0,-14);
            Transform chest=Find(model.transform,"chest")??Find(model.transform,"spine")??identity;
            var sash=VisualFactory.Shape(chest,PrimitiveType.Cube,"Elite gold bandolier",new Vector3(0,.25f,.2f),new Vector3(.08f,.75f,.06f),Gold);
            sash.transform.localRotation=Quaternion.Euler(0,0,38);
            for(int side=-1;side<=1;side+=2)
                VisualFactory.Shape(chest,PrimitiveType.Sphere,"Elite gold epaulette",new Vector3(side*.33f,.52f,0),new Vector3(.24f,.1f,.24f),Gold);
            Transform hand=Find(model.transform,"handslot.r")??identity;
            var rifle=new GameObject("Elite long rifle").transform;rifle.SetParent(hand,false);rifle.localPosition=new Vector3(.05f,0,.05f);
            var barrel=VisualFactory.Shape(rifle,PrimitiveType.Cylinder,"Rifle barrel",new Vector3(0,.02f,.62f),new Vector3(.065f,.62f,.065f),Steel);
            barrel.transform.localRotation=Quaternion.Euler(90,0,0);
            var stock=VisualFactory.Shape(rifle,PrimitiveType.Cube,"Rifle walnut stock",new Vector3(0,-.04f,-.05f),new Vector3(.12f,.18f,.62f),DarkWood);
            stock.transform.localRotation=Quaternion.Euler(-8,0,0);
            VisualFactory.Shape(rifle,PrimitiveType.Cube,"Rifle brass lock",new Vector3(.065f,.01f,.16f),new Vector3(.03f,.09f,.16f),Gold);
        }

        static void Roarer(GameObject model,int team)
        {
            if(model.transform.Find("Roarer identity"))return;
            var identity=new GameObject("Roarer identity").transform;identity.SetParent(model.transform,false);
            Color cloth=VisualFactory.TeamMaterialColor(team);
            Transform head=Find(model.transform,"head")??identity;
            // Horned war helm and fur mantle distinguish the Roarer from the Mage/Medic silhouettes.
            VisualFactory.Shape(head,PrimitiveType.Sphere,"Roarer iron helm",new Vector3(0,.3f,0),new Vector3(.56f,.32f,.54f),Iron);
            for(int side=-1;side<=1;side+=2)
            {
                var horn=VisualFactory.Cone(head,"Roarer helm horn",new Vector3(side*.26f,.38f,0),.075f,.42f,new Color(.86f,.80f,.64f),6);
                horn.transform.localRotation=Quaternion.Euler(0,0,side*-48);
            }
            Transform chest=Find(model.transform,"chest")??Find(model.transform,"spine")??identity;
            VisualFactory.Shape(chest,PrimitiveType.Sphere,"Roarer fur mantle",new Vector3(0,.5f,-.04f),new Vector3(.95f,.34f,.7f),Fur);
            VisualFactory.Shape(chest,PrimitiveType.Cube,"Roarer team war sash",new Vector3(0,.18f,.25f),new Vector3(.5f,.28f,.05f),cloth);
            Transform hand=Find(model.transform,"handslot.l")??identity;
            var horn2=VisualFactory.Cone(hand,"Roarer war horn",new Vector3(0,.05f,.05f),.12f,.55f,new Color(.82f,.72f,.50f),8);
            horn2.transform.localRotation=Quaternion.Euler(-70,0,0);
            VisualFactory.Shape(hand,PrimitiveType.Cylinder,"Roarer horn gold rim",new Vector3(0,.05f,.05f),new Vector3(.26f,.025f,.26f),Gold);
        }

        /// <summary>Army General: larger mounted commander with a team cape and standard.</summary>
        public static GameObject General(Transform parent,int team,Soldier owner=null)
        {
            var model=owner?MountedKnightView.Create(owner):MountedKnightView.Create(parent,team);
            model.name="ArmyGeneral(Clone)";
            // Measure the mount alone so the standard does not shrink the commander.
            ModelMetrics.MatchStandingHeight(model,UnitKind.ArmyGeneral);
            var rig=model.transform.Find("Mounted motion rig")??model.transform;
            Color cloth=VisualFactory.TeamMaterialColor(team);
            var cape=VisualFactory.Shape(rig,PrimitiveType.Cube,"General team cape",new Vector3(0,1.9f,-.48f),new Vector3(.78f,.95f,.06f),cloth*.85f);
            cape.transform.localRotation=Quaternion.Euler(14,0,0);
            VisualFactory.Shape(rig,PrimitiveType.Cube,"General gold cape clasp",new Vector3(0,2.36f,-.34f),new Vector3(.62f,.08f,.08f),Gold);
            VisualFactory.Shape(rig,PrimitiveType.Sphere,"General gold crown",new Vector3(0,2.9f,-.15f),new Vector3(.42f,.12f,.42f),Gold);
            var pole=VisualFactory.Shape(rig,PrimitiveType.Cylinder,"General standard pole",new Vector3(-.36f,2.7f,-.52f),new Vector3(.05f,1.25f,.05f),DarkWood);
            pole.transform.localRotation=Quaternion.Euler(-6,0,0);
            VisualFactory.Shape(rig,PrimitiveType.Cube,"General team standard",new Vector3(-.36f,3.45f,-.2f),new Vector3(.04f,.5f,.62f),cloth);
            VisualFactory.Shape(rig,PrimitiveType.Cube,"General standard gold trim",new Vector3(-.36f,3.2f,-.2f),new Vector3(.045f,.06f,.62f),Gold);
            return model;
        }

        /// <summary>h00M: a longer field gun on tall wheels with a team gun shield. Returns the recoiling barrel.</summary>
        public static Transform ArtilleryModel(Transform root,Color team)
        {
            Shape(root,PrimitiveType.Cube,"Artillery split trail",new Vector3(0,.34f,-.72f),new Vector3(.32f,.16f,1.35f),DarkWood).transform.localRotation=Quaternion.Euler(-9,0,0);
            Shape(root,PrimitiveType.Cube,"Artillery axle carriage",new Vector3(0,.62f,.05f),new Vector3(1.05f,.26f,.62f),DarkWood);
            Shape(root,PrimitiveType.Cube,"Artillery team gun shield",new Vector3(0,.98f,.36f),new Vector3(1.2f,.62f,.06f),team);
            Shape(root,PrimitiveType.Cube,"Artillery shield rim",new Vector3(0,1.3f,.37f),new Vector3(1.24f,.06f,.07f),Steel);
            for(int side=-1;side<=1;side+=2)
            {
                var wheel=Shape(root,PrimitiveType.Cylinder,"Artillery spoked wheel",new Vector3(side*.66f,.56f,.05f),new Vector3(1.1f,.07f,1.1f),DarkWood);
                wheel.transform.localRotation=Quaternion.Euler(0,0,90);
                var tyre=Shape(root,PrimitiveType.Cylinder,"Artillery iron tyre",new Vector3(side*.7f,.56f,.05f),new Vector3(1.16f,.03f,1.16f),Iron);
                tyre.transform.localRotation=Quaternion.Euler(0,0,90);
                var hub=Shape(root,PrimitiveType.Cylinder,"Artillery wheel hub",new Vector3(side*.76f,.56f,.05f),new Vector3(.22f,.04f,.22f),Steel);
                hub.transform.localRotation=Quaternion.Euler(0,0,90);
            }
            var pivot=new GameObject("Artillery barrel").transform;pivot.SetParent(root,false);pivot.localPosition=new Vector3(0,.86f,.02f);pivot.localRotation=Quaternion.Euler(72,0,0);
            Shape(pivot,PrimitiveType.Cylinder,"Artillery bronze tube",new Vector3(0,.55f,0),new Vector3(.3f,.78f,.3f),new Color(.58f,.44f,.22f));
            Shape(pivot,PrimitiveType.Cylinder,"Artillery muzzle swell",new Vector3(0,1.3f,0),new Vector3(.36f,.07f,.36f),new Color(.62f,.47f,.24f));
            Shape(pivot,PrimitiveType.Cylinder,"Artillery bore",new Vector3(0,1.372f,0),new Vector3(.22f,.003f,.22f),new Color(.025f,.026f,.023f));
            Shape(pivot,PrimitiveType.Sphere,"Artillery cascabel",new Vector3(0,-.25f,0),new Vector3(.26f,.26f,.26f),new Color(.58f,.44f,.22f));
            return pivot;
        }

        /// <summary>h01A: steam-tank style hull, boiler stack and turret. Returns the recoiling barrel.</summary>
        public static Transform TankModel(Transform root,Color team)
        {
            Shape(root,PrimitiveType.Cube,"Tank lower hull",new Vector3(0,.55f,0),new Vector3(1.35f,.5f,1.9f),Iron);
            Shape(root,PrimitiveType.Cube,"Tank riveted upper hull",new Vector3(0,.98f,-.1f),new Vector3(1.15f,.42f,1.45f),Steel);
            Shape(root,PrimitiveType.Cube,"Tank sloped glacis",new Vector3(0,.86f,.78f),new Vector3(1.12f,.1f,.6f),Steel).transform.localRotation=Quaternion.Euler(-32,0,0);
            for(int side=-1;side<=1;side+=2)
            {
                Shape(root,PrimitiveType.Cube,"Tank tread",new Vector3(side*.74f,.36f,0),new Vector3(.28f,.5f,2.05f),new Color(.12f,.12f,.11f));
                Shape(root,PrimitiveType.Cube,"Tank team side plate",new Vector3(side*.63f,.9f,-.1f),new Vector3(.05f,.32f,1.2f),team);
                for(int i=0;i<4;i++)
                {
                    var wheel=Shape(root,PrimitiveType.Cylinder,"Tank road wheel",new Vector3(side*.9f,.32f,-.72f+i*.48f),new Vector3(.36f,.04f,.36f),Iron);
                    wheel.transform.localRotation=Quaternion.Euler(0,0,90);
                }
            }
            var boiler=Shape(root,PrimitiveType.Cylinder,"Tank brass boiler",new Vector3(0,1.08f,-.72f),new Vector3(.62f,.34f,.62f),new Color(.62f,.47f,.24f));
            boiler.transform.localRotation=Quaternion.Euler(90,0,0);
            Shape(root,PrimitiveType.Cylinder,"Tank smoke stack",new Vector3(.32f,1.55f,-.82f),new Vector3(.18f,.42f,.18f),Iron);
            Shape(root,PrimitiveType.Cylinder,"Tank stack cap",new Vector3(.32f,1.98f,-.82f),new Vector3(.26f,.04f,.26f),Iron);
            var turret=new GameObject("Tank turret").transform;turret.SetParent(root,false);turret.localPosition=new Vector3(0,1.3f,.1f);
            Shape(turret,PrimitiveType.Cylinder,"Tank turret drum",Vector3.zero,new Vector3(.82f,.2f,.82f),Steel);
            Shape(turret,PrimitiveType.Cube,"Tank turret team band",new Vector3(0,.02f,0),new Vector3(.84f,.08f,.2f),team);
            Shape(turret,PrimitiveType.Sphere,"Tank hatch",new Vector3(0,.2f,-.12f),new Vector3(.34f,.14f,.34f),Iron);
            var pivot=new GameObject("Tank barrel").transform;pivot.SetParent(turret,false);pivot.localPosition=new Vector3(0,.02f,.32f);pivot.localRotation=Quaternion.Euler(86,0,0);
            Shape(pivot,PrimitiveType.Cylinder,"Tank gun tube",new Vector3(0,.5f,0),new Vector3(.16f,.5f,.16f),Iron);
            Shape(pivot,PrimitiveType.Cylinder,"Tank muzzle brake",new Vector3(0,1.02f,0),new Vector3(.24f,.07f,.24f),Iron);
            return pivot;
        }

        static GameObject Shape(Transform root,PrimitiveType type,string name,Vector3 position,Vector3 size,Color color) => VisualFactory.Shape(root,type,name,position,size,color);

        static Transform Find(Transform root,string name)
        {
            foreach(var child in root.GetComponentsInChildren<Transform>(true))if(child.name==name)return child;
            return null;
        }
    }

    /// <summary>Presentation-only barrel recoil for procedural siege units; combat stays in Soldier.SimTick.</summary>
    public sealed class SiegeUnitView : MonoBehaviour
    {
        Soldier soldier;
        Transform barrel;
        Vector3 rest;

        public static SiegeUnitView Attach(Soldier owner,Transform barrel)
        {
            var view=owner.gameObject.AddComponent<SiegeUnitView>();
            view.soldier=owner;view.barrel=barrel;view.rest=barrel?barrel.localPosition:Vector3.zero;return view;
        }

        void LateUpdate()
        {
            if(!soldier||!barrel)return;
            float progress=soldier.AttackPresentationProgress;
            float kick=progress<0?0:AttackPresentationTiming.ContactPose(progress,AttackPresentationTiming.ContactNormalizedTime(soldier.Kind));
            barrel.localPosition=rest-(barrel.localRotation*Vector3.up)*kick*.22f;
        }
    }
}
