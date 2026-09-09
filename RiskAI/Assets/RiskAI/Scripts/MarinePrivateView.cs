using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Shared pirate presentation for the port pistol unit.</summary>
    public static class MarinePrivateView
    {
        public static void Apply(GameObject model,int team)
        {
            if(!model||model.transform.Find("Marine private identity"))return;
            foreach(var part in model.GetComponentsInChildren<Transform>(true))
                if(part.name=="Rogue_Cape"||part.name=="Rogue_Head_Hooded"||part.name=="1H_Crossbow"||part.name=="2H_Crossbow"||part.name=="Knife_Offhand")
                    part.gameObject.SetActive(false);

            var identity=new GameObject("Marine private identity").transform;
            identity.SetParent(model.transform,false);
            Color skin=new Color(.78f,.54f,.34f),leather=new Color(.20f,.105f,.045f),metal=new Color(.48f,.52f,.53f);
            Color cloth=VisualFactory.TeamMaterialColor(team),linen=new Color(.82f,.76f,.60f);

            Transform head=Find(model.transform,"head")??identity;
            var face=VisualFactory.Shape(head,PrimitiveType.Sphere,"Pirate face",Vector3.zero,new Vector3(.56f,.62f,.54f),skin);
            face.transform.localRotation=Quaternion.identity;
            var brim=VisualFactory.Shape(head,PrimitiveType.Cylinder,"Low pirate hat brim",new Vector3(0,.30f,0),new Vector3(.82f,.045f,.68f),leather);
            brim.transform.localRotation=Quaternion.identity;
            var crown=VisualFactory.Shape(head,PrimitiveType.Cylinder,"Low pirate hat crown",new Vector3(0,.43f,0),new Vector3(.50f,.14f,.44f),leather);
            crown.transform.localRotation=Quaternion.identity;
            VisualFactory.Shape(head,PrimitiveType.Cube,"Pirate hat team band",new Vector3(0,.32f,.25f),new Vector3(.58f,.075f,.035f),cloth);
            var feather=VisualFactory.Shape(head,PrimitiveType.Capsule,"Pirate hat feather",new Vector3(.37f,.65f,-.02f),new Vector3(.075f,.30f,.045f),linen);
            feather.transform.localRotation=Quaternion.Euler(0,0,-24);
            var patch=VisualFactory.Shape(head,PrimitiveType.Sphere,"Pirate eye patch",new Vector3(.17f,.035f,.285f),new Vector3(.15f,.105f,.035f),new Color(.025f,.02f,.015f));
            patch.transform.localRotation=Quaternion.identity;
            var strap=VisualFactory.Shape(head,PrimitiveType.Cube,"Pirate eye patch strap",new Vector3(0,.10f,.272f),new Vector3(.48f,.025f,.018f),new Color(.025f,.02f,.015f));
            strap.transform.localRotation=Quaternion.Euler(0,0,10);
            VisualFactory.Shape(head,PrimitiveType.Cube,"Pirate neck scarf",new Vector3(0,-.29f,.08f),new Vector3(.47f,.10f,.22f),linen);

            Transform hand=Find(model.transform,"handslot.r")??identity;
            Transform source=Find(model.transform,"1H_Crossbow");
            var pistol=new GameObject("Short flintlock pistol").transform;pistol.SetParent(hand,false);
            if(source){pistol.localPosition=source.localPosition;pistol.localRotation=source.localRotation;}
            else pistol.localPosition=new Vector3(.1f,-.01f,0);
            var barrel=VisualFactory.Shape(pistol,PrimitiveType.Cylinder,"Pistol barrel",new Vector3(0,0,.31f),new Vector3(.11f,.34f,.11f),metal);
            barrel.transform.localRotation=Quaternion.Euler(90,0,0);
            var muzzle=VisualFactory.Shape(pistol,PrimitiveType.Cylinder,"Pistol muzzle",new Vector3(0,0,.64f),new Vector3(.15f,.08f,.15f),metal);
            muzzle.transform.localRotation=Quaternion.Euler(90,0,0);
            var stock=VisualFactory.Shape(pistol,PrimitiveType.Cube,"Pistol stock",new Vector3(0,-.10f,.02f),new Vector3(.16f,.28f,.22f),leather);
            stock.transform.localRotation=Quaternion.Euler(-18,0,0);
            VisualFactory.Shape(pistol,PrimitiveType.Cube,"Pistol brass lock",new Vector3(.10f,.02f,.13f),new Vector3(.035f,.14f,.18f),new Color(.67f,.45f,.16f));
        }

        static Transform Find(Transform root,string name)
        {
            foreach(var child in root.GetComponentsInChildren<Transform>(true))if(child.name==name)return child;
            return null;
        }
    }
}
