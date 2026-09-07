using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskAI
{
    // One shared nine-sliced material texture, with retained vector fallback.
    // No Update loop, per-panel texture allocation or gameplay dependency.
    public sealed class RtsOrnamentPanel : VisualElement
    {
        static Texture2D frame;
        public RtsOrnamentPanel()
        {
            if(!frame)frame=Resources.Load<Texture2D>("UI/WarTableFrame-v20");
            if(frame)
            {
                style.backgroundImage=frame;
                int corner=Mathf.RoundToInt(frame.width*.175f);
                style.unitySliceTop=corner;style.unitySliceBottom=corner;
                style.unitySliceLeft=corner;style.unitySliceRight=corner;
                style.unitySliceScale=17f/corner;
            }
            else generateVisualContent+=Paint;
        }
        void Paint(MeshGenerationContext context)
        {
            var r=localBound;if(r.width<12||r.height<12)return;
            var p=context.painter2D;float w=r.width,h=r.height;
            RtsOrnamentDrawing.Box(p,2,2,w-4,6,new Color(.29f,.20f,.105f));
            RtsOrnamentDrawing.Box(p,2,h-7,w-4,5,new Color(.20f,.14f,.075f));
            // Long grain confined to rails, leaving text on quiet charcoal.
            for(int i=0;i<3;i++)
                RtsOrnamentDrawing.Line(p,new Vector2(5,3+i*1.6f),new Vector2(w-5,3+i*1.6f),new Color(.40f,.30f,.16f,.65f),.7f);
            RtsOrnamentDrawing.Line(p,new Vector2(8,10),new Vector2(w-8,10),new Color(.66f,.52f,.28f),1);
            RtsOrnamentDrawing.Line(p,new Vector2(8,h-10),new Vector2(w-8,h-10),new Color(.28f,.25f,.16f),1);
            // Iron plates and brass rivets give the frame weight at every scale.
            for(int corner=0;corner<4;corner++)
            {
                float x=(corner%2==0)?2:w-16,y=corner<2?2:h-16;
                RtsOrnamentDrawing.Box(p,x,y,14,14,new Color(.19f,.20f,.18f));
                RtsOrnamentDrawing.Line(p,new Vector2(x,y),new Vector2(x+14,y),new Color(.47f,.46f,.37f),1);
                RtsOrnamentDrawing.Diamond(p,new Vector2(x+7,y+7),2,RtsUiStyle.Bronze);
            }
            if(w>160)RtsOrnamentDrawing.Diamond(p,new Vector2(w*.5f,6),4,RtsUiStyle.Gold);
        }
    }

    sealed class RtsOrnamentButton : Button
    {
        bool hovered;
        public RtsOrnamentButton(Action action):base(action)
        {
            generateVisualContent+=Paint;
            RegisterCallback<PointerEnterEvent>(_=>{hovered=true;MarkDirtyRepaint();});
            RegisterCallback<PointerLeaveEvent>(_=>{hovered=false;MarkDirtyRepaint();});
        }
        void Paint(MeshGenerationContext context)
        {
            var r=localBound;if(r.width<8||r.height<8)return;
            var p=context.painter2D;float w=r.width,h=r.height;
            var bright=hovered?RtsUiStyle.Gold:new Color(.47f,.39f,.23f);
            RtsOrnamentDrawing.Line(p,new Vector2(3,3),new Vector2(w-3,3),bright,1);
            RtsOrnamentDrawing.Line(p,new Vector2(3,3),new Vector2(3,h-3),bright,1);
            RtsOrnamentDrawing.Line(p,new Vector2(3,h-3),new Vector2(w-3,h-3),new Color(.045f,.04f,.025f),2);
            RtsOrnamentDrawing.Line(p,new Vector2(w-3,3),new Vector2(w-3,h-3),new Color(.045f,.04f,.025f),2);
        }
    }

    /// <summary>Decorative heraldry for setup cards; never represents playable geography.</summary>
    public sealed class RtsHeraldicSeal : VisualElement
    {
        readonly int motif;
        readonly Color accent;
        public RtsHeraldicSeal(int motif,Color accent)
        {
            this.motif=motif;this.accent=accent;pickingMode=PickingMode.Ignore;
            style.width=76;style.height=86;style.flexShrink=0;
            generateVisualContent+=Paint;
        }
        void Paint(MeshGenerationContext context)
        {
            var r=contentRect;if(r.width<10||r.height<10)return;
            var p=context.painter2D;float w=r.width,h=r.height;
            p.fillColor=new Color(.055f,.07f,.068f);p.strokeColor=RtsUiStyle.Bronze;p.lineWidth=2;
            p.BeginPath();p.MoveTo(new Vector2(w*.12f,h*.08f));p.LineTo(new Vector2(w*.88f,h*.08f));
            p.LineTo(new Vector2(w*.84f,h*.64f));p.LineTo(new Vector2(w*.5f,h*.94f));
            p.LineTo(new Vector2(w*.16f,h*.64f));p.ClosePath();p.Fill();p.Stroke();
            p.strokeColor=accent;p.lineWidth=2;
            if(motif==0||motif==1)
            {
                // Pine / river heraldry, drawn as marks on the shield.
                for(int tier=0;tier<3;tier++)
                {
                    float y=h*(.23f+tier*.13f),half=w*(.1f+tier*.05f);
                    p.fillColor=accent;p.BeginPath();p.MoveTo(new Vector2(w*.5f,y));
                    p.LineTo(new Vector2(w*.5f+half,y+h*.2f));p.LineTo(new Vector2(w*.5f-half,y+h*.2f));p.ClosePath();p.Fill();
                }
                if(motif==1)RtsOrnamentDrawing.Line(p,new Vector2(w*.27f,h*.73f),new Vector2(w*.68f,h*.67f),new Color(.42f,.70f,.71f),3);
            }
            else
            {
                Vector2 c=new Vector2(w*.5f,h*.43f);
                RtsOrnamentDrawing.Line(p,new Vector2(c.x,h*.18f),new Vector2(c.x,h*.69f),accent,2);
                RtsOrnamentDrawing.Line(p,new Vector2(w*.27f,c.y),new Vector2(w*.73f,c.y),accent,2);
                RtsOrnamentDrawing.Diamond(p,c,w*.19f,accent);
                RtsOrnamentDrawing.Diamond(p,c,w*.09f,new Color(.065f,.075f,.068f));
                if(motif==3)
                    for(int i=0;i<2;i++)RtsOrnamentDrawing.Line(p,new Vector2(w*.30f,h*(.69f+i*.065f)),new Vector2(w*.7f,h*(.69f+i*.065f)),accent,1);
            }
        }
    }

    static class RtsOrnamentDrawing
    {
        public static void Box(Painter2D p,float x,float y,float w,float h,Color color)
        {
            p.fillColor=color;p.BeginPath();p.MoveTo(new Vector2(x,y));p.LineTo(new Vector2(x+w,y));
            p.LineTo(new Vector2(x+w,y+h));p.LineTo(new Vector2(x,y+h));p.ClosePath();p.Fill();
        }
        public static void Line(Painter2D p,Vector2 a,Vector2 b,Color color,float width)
        { p.strokeColor=color;p.lineWidth=width;p.BeginPath();p.MoveTo(a);p.LineTo(b);p.Stroke(); }
        public static void Diamond(Painter2D p,Vector2 c,float size,Color color)
        {
            p.fillColor=color;p.BeginPath();p.MoveTo(c+Vector2.up*size);p.LineTo(c+Vector2.right*size);
            p.LineTo(c+Vector2.down*size);p.LineTo(c+Vector2.left*size);p.ClosePath();p.Fill();
        }
    }
}
