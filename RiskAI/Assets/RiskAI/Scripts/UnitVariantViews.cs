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

        /// <summary>units.json presentation.portraitSource is Variant: the art setup renders the unit view and does not claim a prefab.</summary>
        public static bool HasVariantPortrait(UnitKind kind) =>
            UnitCatalog.Get(kind).PortraitSource == PortraitSource.Variant;

        /// <summary>Land unit whose portrait is the shared model (or the Mortar cart), so the art setup prepares that prefab.</summary>
        public static bool PreparesBaseModel(UnitKind kind)
        {
            ref readonly var type = ref UnitCatalog.Get(kind);
            return type.Domain == UnitDomain.Land && type.PortraitSource == PortraitSource.Model;
        }

        /// <summary>Variant portraits in catalog order. The art setup and the editor test both call this.</summary>
        public static void CollectVariantPortraits(List<UnitKind> kinds)
        {
            kinds.Clear();
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
                if (HasVariantPortrait(kind)) kinds.Add(kind);
        }

        /// <summary>
        /// First land claimant of each model name, catalog order. A repeated model is prepared once.
        /// Mortar stays in this list: its portrait is the procedural cart, not a variant view.
        /// </summary>
        public static void CollectBasePreparations(List<UnitKind> kinds, List<string> models)
        {
            kinds.Clear();
            models.Clear();
            var seen = new HashSet<string>();
            foreach (UnitKind kind in System.Enum.GetValues(typeof(UnitKind)))
            {
                if (!PreparesBaseModel(kind)) continue;
                string name = UnitCatalog.Get(kind).Model;
                if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                kinds.Add(kind);
                models.Add(name);
            }
        }

        /// <summary>Resources path of the unit portrait (units.json portrait, then portraitFallback until the art setup renders it).</summary>
        public static string PortraitResource(UnitKind kind) => Resolve(UnitCatalog.Get(kind).PortraitName, UnitCatalog.Get(kind).PortraitFallback);

        /// <summary>
        /// The same path as <see cref="PortraitResource"/>. Throws when neither the portrait nor its
        /// fallback is in Resources, so a missing PNG is reported in this one place.
        /// </summary>
        public static string RequirePortrait(UnitKind kind)
        {
            ref readonly var type = ref UnitCatalog.Get(kind);
            string path = Resolve(type.PortraitName, type.PortraitFallback);
            if (!Resources.Load<Texture2D>(path))
                throw new System.InvalidOperationException("Portrait PNG is missing: Resources/" + path + ".png (unit " + type.Id + ").");
            return path;
        }

        static string Resolve(string preferred, string fallback)
        {
            if (resolvedPortraits.TryGetValue(preferred, out var path)) return path;
            path = "Portraits/" + preferred;
            if (preferred != fallback && !Resources.Load<Texture2D>(path)) path = "Portraits/" + fallback;
            resolvedPortraits[preferred] = path;
            return path;
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

        /// <summary>
        /// h00I Roarer: a war herald on the Rogue base (hood off, no wizard hat): horned iron cap
        /// and beard, fur mantle, a great curved war horn in hand, a drum on the hip and a tall
        /// swallow-tailed team banner on the back that marks him at any zoom.
        /// Bone frames (KayKit rig): chest/head +Z forward, +Y up, +X right; handslot.r +X up, +Y forward.
        /// </summary>
        static void Roarer(GameObject model,int team)
        {
            if(model.transform.Find("Roarer identity"))return;
            foreach(var part in model.GetComponentsInChildren<Transform>(true))
                if(part.name=="1H_Crossbow"||part.name=="2H_Crossbow"||part.name=="Knife_Offhand"||part.name=="Rogue_Head_Hooded"||part.name=="Procedural crossbow")
                    part.gameObject.SetActive(false);
            var identity=new GameObject("Roarer identity").transform;identity.SetParent(model.transform,false);
            Color cloth=VisualFactory.TeamMaterialColor(team),beard=new Color(.46f,.22f,.08f),ivory=new Color(.9f,.84f,.66f),furLight=new Color(.62f,.5f,.36f);
            // KayKit heads are oversized; match that proportion with a scaled head rig.
            var head=new GameObject("Roarer head rig").transform;head.SetParent(Find(model.transform,"head")??identity,false);
            head.localPosition=new Vector3(0,.1f,0);head.localScale=Vector3.one*1.35f;
            // The hood carried the head; rebuild it bare (like the Marine) with a beard and horned cap.
            Shape(head,PrimitiveType.Sphere,"Roarer face",new Vector3(0,-.02f,.02f),new Vector3(.5f,.56f,.48f),new Color(.8f,.56f,.38f));
            Shape(head,PrimitiveType.Cube,"Roarer eyes",new Vector3(0,.03f,.245f),new Vector3(.24f,.045f,.02f),new Color(.05f,.04f,.04f));
            var beardShape=Shape(head,PrimitiveType.Sphere,"Roarer braided beard",new Vector3(0,-.2f,.17f),new Vector3(.38f,.32f,.24f),beard);
            beardShape.transform.localRotation=Quaternion.Euler(14,0,0);
            Shape(head,PrimitiveType.Cylinder,"Roarer beard ring",new Vector3(0,-.36f,.22f),new Vector3(.08f,.03f,.08f),Gold);
            Shape(head,PrimitiveType.Sphere,"Roarer iron cap",new Vector3(0,.19f,0),new Vector3(.55f,.38f,.53f),Iron);
            Shape(head,PrimitiveType.Cylinder,"Roarer cap gold band",new Vector3(0,.1f,0),new Vector3(.57f,.03f,.55f),Gold);
            for(int side=-1;side<=1;side+=2)
            {
                // Curved horns: a thick base cone swept out and a thinner tip bent upward.
                var hornBase=VisualFactory.Cone(head,"Roarer cap horn",new Vector3(side*.25f,.22f,0),.075f,.24f,ivory,6);
                hornBase.transform.localRotation=Quaternion.Euler(0,0,side*-68);
                var hornTip=VisualFactory.Cone(head,"Roarer cap horn tip",new Vector3(side*.43f,.3f,0),.048f,.22f,ivory,6);
                hornTip.transform.localRotation=Quaternion.Euler(0,0,side*-14);
            }
            Transform chest=Find(model.transform,"chest")??Find(model.transform,"spine")??identity;
            Shape(chest,PrimitiveType.Sphere,"Roarer fur mantle",new Vector3(0,.1f,-.04f),new Vector3(1.12f,.4f,.82f),Fur);
            Shape(chest,PrimitiveType.Sphere,"Roarer fur collar",new Vector3(0,.18f,.06f),new Vector3(.74f,.18f,.56f),furLight);
            var baldric=Shape(chest,PrimitiveType.Cube,"Roarer leather baldric",new Vector3(0,-.2f,.3f),new Vector3(.1f,.8f,.05f),new Color(.3f,.16f,.07f));
            baldric.transform.localRotation=Quaternion.Euler(0,0,-36);
            // Back banner: pole, cross bar, gold finial and a tall swallow-tailed team banner.
            Vector3 foot=new Vector3(.18f,-.55f,-.44f),top=new Vector3(.18f,1.95f,-.5f);
            Segment(chest,"Roarer banner pole",foot,top,.05f,DarkWood);
            VisualFactory.Cone(chest,"Roarer banner finial",top,.08f,.22f,Gold,6);
            Shape(chest,PrimitiveType.Cube,"Roarer banner bar",new Vector3(.46f,1.82f,-.5f),new Vector3(.62f,.05f,.05f),Gold);
            Shape(chest,PrimitiveType.Cube,"Roarer team banner",new Vector3(.48f,1.42f,-.51f),new Vector3(.58f,.78f,.03f),cloth);
            for(int side=-1;side<=1;side+=2)
            {
                var tail=Shape(chest,PrimitiveType.Cube,"Roarer banner tail",new Vector3(.48f+side*.16f,.94f,-.51f),new Vector3(.24f,.3f,.03f),cloth);
                tail.transform.localRotation=Quaternion.Euler(0,0,side*12);
            }
            Shape(chest,PrimitiveType.Cube,"Roarer banner emblem",new Vector3(.48f,1.46f,-.53f),new Vector3(.2f,.2f,.02f),Gold).transform.localRotation=Quaternion.Euler(0,0,45);
            // War drum on the left hip.
            var drum=Shape(chest,PrimitiveType.Cylinder,"Roarer war drum",new Vector3(-.5f,-.55f,.08f),new Vector3(.4f,.14f,.4f),new Color(.55f,.36f,.18f));
            drum.transform.localRotation=Quaternion.Euler(0,0,78);
            var band=Shape(chest,PrimitiveType.Cylinder,"Roarer drum team band",new Vector3(-.5f,-.55f,.08f),new Vector3(.42f,.05f,.42f),cloth);
            band.transform.localRotation=Quaternion.Euler(0,0,78);
            // Great curved war horn in the right hand: tapering ivory segments sweeping forward
            // and up to a gold-rimmed bell.
            Transform hand=Find(model.transform,"handslot.r")??identity;
            var horn=new GameObject("Roarer war horn").transform;horn.SetParent(hand,false);
            Vector3 previous=new Vector3(-.05f,-.12f,0);float radius=.045f;
            for(int i=1;i<=6;i++)
            {
                float t=i/6f;var point=new Vector3(-.05f+.42f*t*t,-.12f+.7f*t,0);
                Segment(horn,"Roarer horn segment",previous,point,radius,ivory);
                previous=point;radius+=.02f;
            }
            var bellDirection=new Vector3(.84f,.7f,0).normalized;
            Shape(horn,PrimitiveType.Cylinder,"Roarer horn gold ring",new Vector3(.05f,.24f,0),new Vector3(.16f,.025f,.16f),Gold).transform.localRotation=Quaternion.FromToRotation(Vector3.up,new Vector3(.4f,.7f,0).normalized);
            var bell=VisualFactory.Cone(horn,"Roarer horn bell",previous+bellDirection*.2f,.24f,.26f,ivory,10);
            bell.transform.localRotation=Quaternion.FromToRotation(Vector3.up,-bellDirection);
            Shape(horn,PrimitiveType.Cylinder,"Roarer horn bell rim",previous+bellDirection*.2f,new Vector3(.5f,.03f,.5f),Gold).transform.localRotation=Quaternion.FromToRotation(Vector3.up,bellDirection);
        }

        static void Segment(Transform parent,string name,Vector3 from,Vector3 to,float radius,Color color)
        {
            var go=Shape(parent,PrimitiveType.Cylinder,name,(from+to)*.5f,new Vector3(radius*2,(to-from).magnitude*.5f,radius*2),color);
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(to-from).normalized);
        }

        /// <summary>Army General: mounted commander on a black charger, gilded armour, crown and command standard.</summary>
        public static GameObject General(Transform parent,int team,Soldier owner=null) =>
            MountedKnightView.CreateVariant(owner?owner.transform:parent,team,UnitKind.ArmyGeneral,owner);

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
            float kick=progress<0?0:AttackPresentationTiming.ContactPose(progress,UnitCatalog.Get(soldier.Kind).AttackContact);
            barrel.localPosition=rest-(barrel.localRotation*Vector3.up)*kick*.22f;
        }
    }
}
