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
            Color skin=new Color(.78f,.54f,.34f),leather=new Color(.16f,.075f,.035f),metal=new Color(.48f,.52f,.53f);
            Color cloth=VisualFactory.TeamMaterialColor(team),linen=new Color(.86f,.82f,.68f),navy=new Color(.075f,.12f,.17f);

            Transform head=Find(model.transform,"head")??identity;
            var face=VisualFactory.Shape(head,PrimitiveType.Sphere,"Sailor face",new Vector3(0,-.03f,.015f),new Vector3(.46f,.54f,.45f),skin);
            face.transform.localRotation=Quaternion.identity;
            // A compact three-corner hat reads as a sailor at game distance without
            // the oversized round head and decorative clutter of the first pass.
            var crown=VisualFactory.Shape(head,PrimitiveType.Cylinder,"Tricorn crown",new Vector3(0,.31f,0),new Vector3(.46f,.15f,.42f),navy);
            crown.transform.localRotation=Quaternion.identity;
            for(int side=-1;side<=1;side+=2)
            {
                var brim=VisualFactory.Shape(head,PrimitiveType.Cube,"Tricorn raised brim",new Vector3(side*.23f,.32f,.01f),new Vector3(.36f,.055f,.62f),navy);
                brim.transform.localRotation=Quaternion.Euler(0,side*18,side*25);
            }
            var front=VisualFactory.Shape(head,PrimitiveType.Cube,"Tricorn front brim",new Vector3(0,.33f,.22f),new Vector3(.62f,.055f,.30f),navy);
            front.transform.localRotation=Quaternion.Euler(-18,0,0);
            VisualFactory.Shape(head,PrimitiveType.Cube,"Tricorn team cockade",new Vector3(.18f,.38f,.34f),new Vector3(.15f,.15f,.035f),cloth);
            VisualFactory.Shape(head,PrimitiveType.Cube,"Sailor neck cloth",new Vector3(0,-.29f,.08f),new Vector3(.40f,.09f,.20f),linen);

            Transform hand=Find(model.transform,"handslot.r")??identity;
            Transform source=Find(model.transform,"1H_Crossbow");
            var pistol=new GameObject("Short flintlock pistol").transform;pistol.SetParent(hand,false);
            if(source){pistol.localPosition=source.localPosition;pistol.localRotation=source.localRotation;}
            else pistol.localPosition=new Vector3(.1f,-.01f,0);
            var barrel=VisualFactory.Shape(pistol,PrimitiveType.Cylinder,"Pistol barrel",new Vector3(0,0,.27f),new Vector3(.085f,.30f,.085f),metal);
            barrel.transform.localRotation=Quaternion.Euler(90,0,0);
            var muzzle=VisualFactory.Shape(pistol,PrimitiveType.Cylinder,"Pistol muzzle",new Vector3(0,0,.56f),new Vector3(.12f,.065f,.12f),metal);
            muzzle.transform.localRotation=Quaternion.Euler(90,0,0);
            var stock=VisualFactory.Shape(pistol,PrimitiveType.Cube,"Pistol stock",new Vector3(0,-.09f,.01f),new Vector3(.14f,.25f,.19f),leather);
            stock.transform.localRotation=Quaternion.Euler(-18,0,0);
            VisualFactory.Shape(pistol,PrimitiveType.Cube,"Pistol brass lock",new Vector3(.08f,.01f,.12f),new Vector3(.028f,.11f,.15f),new Color(.67f,.45f,.16f));
        }

        static Transform Find(Transform root,string name)
        {
            foreach(var child in root.GetComponentsInChildren<Transform>(true))if(child.name==name)return child;
            return null;
        }
    }
}
