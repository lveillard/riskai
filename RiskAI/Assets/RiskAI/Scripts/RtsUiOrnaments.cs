using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskAI
{
    public enum RtsHudGlyph { City,Sword,Shield,Move,Patrol,Stop,Focus,Board,Unload }

    /// <summary>Small vector marks shared by resource counts, rankings and direct commands.</summary>
    public sealed class RtsHudIcon : VisualElement
    {
        readonly RtsHudGlyph glyph;
        public RtsHudIcon(RtsHudGlyph glyph)
        {
            this.glyph=glyph;name="HUD icon "+glyph;pickingMode=PickingMode.Ignore;
            style.width=24;style.height=24;style.flexShrink=0;generateVisualContent+=Paint;
        }
        void Paint(MeshGenerationContext context)
        {
            var p=context.painter2D;var r=contentRect;
            if(r.width<2||r.height<2)return;
            Vector2 At(float x,float y)=>new Vector2(r.x+x*r.width,r.y+y*r.height);
            void Line(float x,float y,float xx,float yy)=>RtsOrnamentDrawing.Line(p,At(x,y),At(xx,yy),RtsUiStyle.Gold,1.7f);
            void Box(float x,float y,float w,float h)=>RtsOrnamentDrawing.Box(p,r.x+x*r.width,r.y+y*r.height,w*r.width,h*r.height,RtsUiStyle.Gold);
            void Arrow(float y,bool right)
            {
                float end=right?.83f:.17f,start=right?.17f:.83f,back=right?.65f:.35f;
                Line(start,y,end,y);Line(end,y,back,y-.17f);Line(end,y,back,y+.17f);
            }
            switch(glyph)
            {
                case RtsHudGlyph.City:
                    Box(.15f,.36f,.7f,.48f);Box(.15f,.16f,.17f,.27f);Box(.42f,.16f,.16f,.27f);Box(.68f,.16f,.17f,.27f);
                    RtsOrnamentDrawing.Box(p,r.x+r.width*.43f,r.y+r.height*.56f,r.width*.14f,r.height*.28f,new Color(.07f,.08f,.07f));break;
                case RtsHudGlyph.Sword:
                    Line(.23f,.8f,.8f,.18f);Line(.19f,.6f,.42f,.83f);Line(.8f,.18f,.61f,.24f);Line(.8f,.18f,.76f,.4f);break;
                case RtsHudGlyph.Shield:
                    p.strokeColor=RtsUiStyle.Gold;p.lineWidth=1.7f;p.BeginPath();p.MoveTo(At(.2f,.15f));p.LineTo(At(.8f,.15f));
                    p.LineTo(At(.76f,.59f));p.LineTo(At(.5f,.87f));p.LineTo(At(.24f,.59f));p.ClosePath();p.Stroke();Line(.5f,.2f,.5f,.7f);break;
                case RtsHudGlyph.Move:Arrow(.5f,true);break;
                case RtsHudGlyph.Patrol:Arrow(.29f,true);Arrow(.71f,false);break;
                case RtsHudGlyph.Stop:Box(.23f,.23f,.54f,.54f);break;
                case RtsHudGlyph.Focus:
                    Line(.12f,.12f,.35f,.12f);Line(.12f,.12f,.12f,.35f);Line(.88f,.12f,.65f,.12f);Line(.88f,.12f,.88f,.35f);
                    Line(.12f,.88f,.35f,.88f);Line(.12f,.88f,.12f,.65f);Line(.88f,.88f,.65f,.88f);Line(.88f,.88f,.88f,.65f);
                    RtsOrnamentDrawing.Diamond(p,At(.5f,.5f),r.width*.12f,RtsUiStyle.Gold);break;
                case RtsHudGlyph.Board:case RtsHudGlyph.Unload:
                    Line(.13f,.68f,.28f,.85f);Line(.28f,.85f,.72f,.85f);Line(.72f,.85f,.87f,.68f);
                    Arrow(.35f,glyph==RtsHudGlyph.Board);break;
            }
        }
    }

    /// <summary>Shared retained coin mark; sharp at every UI scale without an emoji font.</summary>
    public sealed class RtsGoldIcon : VisualElement
    {
        public RtsGoldIcon()
        {
            name="Gold coin icon";tooltip="Oro";pickingMode=PickingMode.Ignore;
            style.width=23;style.height=23;style.flexShrink=0;
            generateVisualContent+=Paint;
        }
        void Paint(MeshGenerationContext context)
        {
            var p=context.painter2D;var r=contentRect;
            var center=r.center;float radius=Mathf.Min(r.width,r.height)*.37f;
            Ring(p,center+new Vector2(1,2),radius,new Color(.48f,.27f,.055f),new Color(.23f,.13f,.04f));
            Ring(p,center,radius,new Color(.92f,.66f,.17f),new Color(1f,.84f,.39f));
            Ring(p,center,radius*.69f,new Color(.82f,.51f,.095f),new Color(.64f,.37f,.055f));
            RtsOrnamentDrawing.Diamond(p,center,radius*.4f,new Color(1,.84f,.38f));
        }
        static void Ring(Painter2D p,Vector2 center,float radius,Color fill,Color edge)
        {
            p.fillColor=fill;p.strokeColor=edge;p.lineWidth=1;p.BeginPath();
            for(int i=0;i<24;i++)
            {
                float angle=i*Mathf.PI/12;var point=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                if(i==0)p.MoveTo(point);else p.LineTo(point);
            }
            p.ClosePath();p.Fill();p.Stroke();
        }
    }

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
