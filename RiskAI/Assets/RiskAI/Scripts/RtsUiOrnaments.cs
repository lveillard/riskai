using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskAI
{
    public enum RtsGlyph
    {
        City, Port, Sword, Shield, Move, Patrol, Stop, Focus, Board, Unload,
        Speaker, Note, Ranking, Map, Chat, Close, Queue, ChevronUp, ChevronDown, Hand
    }

    /// <summary>
    /// The one HUD icon system: commands, resources, ranking headers, quick bar and drawer marks.
    /// Bold filled silhouettes with a dark outline, drawn square and centred in whatever box the
    /// caller gives, so one glyph reads the same from 12 to 40 px.
    /// </summary>
    public sealed class RtsIcon : VisualElement
    {
        static readonly Color Ink = new Color(.93f, .84f, .6f), Off = new Color(.55f, .55f, .5f);
        static readonly Color Strike = new Color(1f, .42f, .36f), Outline = new Color(.05f, .04f, .025f, .92f), Cut = new Color(.11f, .09f, .055f);
        RtsGlyph glyph;
        bool struck, active;

        public RtsIcon(RtsGlyph glyph, float size = 22)
        {
            this.glyph = glyph; name = "HUD icon " + glyph; pickingMode = PickingMode.Ignore;
            style.width = size; style.height = size; style.flexShrink = 0; generateVisualContent += Paint;
        }

        public RtsGlyph Glyph { get => glyph; set { if (glyph == value) return; glyph = value; name = "HUD icon " + value; MarkDirtyRepaint(); } }
        /// <summary>Greyed and struck through: the feature is off.</summary>
        public bool Struck { get => struck; set { if (struck == value) return; struck = value; MarkDirtyRepaint(); } }
        /// <summary>Gold: the toggle is on.</summary>
        public bool Active { get => active; set { if (active == value) return; active = value; MarkDirtyRepaint(); } }

        void Paint(MeshGenerationContext context)
        {
            var p = context.painter2D; var r = contentRect;
            float s = Mathf.Min(r.width, r.height); if (s < 4) return;
            float ox = r.x + (r.width - s) * .5f, oy = r.y + (r.height - s) * .5f;
            float edge = Mathf.Max(1.2f, s * .09f);
            var ink = struck ? Off : active ? RtsUiStyle.Gold : Ink;
            var shade = new Color(ink.r * .74f, ink.g * .74f, ink.b * .74f, 1);
            p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;

            Vector2 P(float x, float y) => new Vector2(ox + x * s, oy + y * s);
            void Begin(float x, float y) { p.BeginPath(); p.MoveTo(P(x, y)); }
            void To(float x, float y) => p.LineTo(P(x, y));
            // Outline first, then the fill over its inner half: a crisp dark rim on any background.
            void Solid(Color fill) { p.ClosePath(); p.strokeColor = Outline; p.lineWidth = edge; p.Stroke(); p.fillColor = fill; p.Fill(); }
            void Flat(Color fill) { p.ClosePath(); p.fillColor = fill; p.Fill(); }
            void Rect(float x, float y, float w, float h, Color fill) { Begin(x, y); To(x + w, y); To(x + w, y + h); To(x, y + h); Solid(fill); }
            void Disc(float x, float y, float radius, Color fill, bool outlined = true)
            {
                p.BeginPath(); p.Arc(P(x, y), radius * s, Angle.Degrees(0), Angle.Degrees(360));
                if (outlined) Solid(fill); else Flat(fill);
            }
            void Bar(float ax, float ay, float bx, float by, float width, Color fill, bool outlined = true)
            {
                Vector2 a = P(ax, ay), b = P(bx, by), d = (b - a).normalized, n = new Vector2(-d.y, d.x) * width * s * .5f;
                p.BeginPath(); p.MoveTo(a + n); p.LineTo(b + n); p.LineTo(b - n); p.LineTo(a - n);
                if (outlined) Solid(fill); else Flat(fill);
            }
            void Curve(float cx, float cy, float radius, float from, float to, float width)
            {
                p.BeginPath(); p.Arc(P(cx, cy), radius * s, Angle.Degrees(from), Angle.Degrees(to));
                p.strokeColor = Outline; p.lineWidth = width * s + edge; p.Stroke();
                p.strokeColor = ink; p.lineWidth = width * s; p.Stroke();
            }
            void Arrow(float y, bool right, float half, float head, float from, float to)
            {
                float tip = right ? to : from, tail = right ? from : to, neck = tip + (right ? -1 : 1) * head * 1.25f;
                Begin(tail, y - half); To(neck, y - half); To(neck, y - head); To(tip, y);
                To(neck, y + head); To(neck, y + half); To(tail, y + half); Solid(ink);
            }
            void VerticalArrow(float x, bool up, float from, float to)
            {
                float tip = up ? from : to, tail = up ? to : from, neck = tip + (up ? 1 : -1) * .16f;
                Begin(x - .055f, tail); To(x - .055f, neck); To(x - .13f, neck); To(x, tip);
                To(x + .13f, neck); To(x + .055f, neck); To(x + .055f, tail); Solid(ink);
            }
            void Anchor(float cx, float k)
            {
                float X(float x) => cx + (x - .5f) * k;
                float Y(float y) => .5f + (y - .5f) * k;
                Curve(X(.5f), Y(.54f), .27f * k, 25, 155, .09f * k);
                Bar(X(.5f), Y(.2f), X(.5f), Y(.82f), .1f * k, ink);
                Bar(X(.3f), Y(.32f), X(.7f), Y(.32f), .09f * k, ink);
                Disc(X(.5f), Y(.14f), .07f * k, ink);
                Begin(X(.71f), Y(.6f)); To(X(.86f), Y(.56f)); To(X(.8f), Y(.73f)); Solid(ink);
                Begin(X(.29f), Y(.6f)); To(X(.14f), Y(.56f)); To(X(.2f), Y(.73f)); Solid(ink);
            }
            void Chevron(bool up)
            {
                float Y(float y) => up ? y : 1 - y;
                Begin(.1f, Y(.72f)); To(.5f, Y(.28f)); To(.9f, Y(.72f)); To(.75f, Y(.86f)); To(.5f, Y(.58f)); To(.25f, Y(.86f)); Solid(ink);
            }

            switch (glyph)
            {
                case RtsGlyph.City:
                    Rect(.16f, .4f, .68f, .48f, ink);
                    Rect(.16f, .2f, .16f, .22f, ink); Rect(.42f, .2f, .16f, .22f, ink); Rect(.68f, .2f, .16f, .22f, ink);
                    Begin(.41f, .88f); To(.41f, .66f); To(.59f, .66f); To(.59f, .88f); Flat(Cut); Disc(.5f, .66f, .09f, Cut, false);
                    break;
                case RtsGlyph.Port: Anchor(.5f, 1); break;
                case RtsGlyph.Sword:
                    // Blade along the diagonal, a fuller groove, then grip, guard and pommel on top.
                    Begin(.42f, .7f); To(.845f, .275f); To(.88f, .12f); To(.725f, .155f); To(.3f, .58f); Solid(ink);
                    Bar(.43f, .57f, .79f, .21f, .03f, shade, false);
                    Bar(.35f, .65f, .19f, .81f, .1f, shade);
                    Bar(.2f, .48f, .52f, .8f, .12f, ink);
                    Disc(.16f, .84f, .085f, ink);
                    break;
                case RtsGlyph.Shield:
                    Begin(.18f, .12f); To(.82f, .12f); To(.8f, .5f); To(.72f, .7f); To(.5f, .9f); To(.28f, .7f); To(.2f, .5f); Solid(ink);
                    Bar(.5f, .2f, .5f, .78f, .08f, Cut, false); Bar(.27f, .4f, .73f, .4f, .08f, Cut, false);
                    break;
                case RtsGlyph.Move: Arrow(.5f, true, .1f, .3f, .1f, .9f); break;
                case RtsGlyph.Patrol: Arrow(.3f, true, .06f, .18f, .12f, .88f); Arrow(.7f, false, .06f, .18f, .12f, .88f); break;
                case RtsGlyph.Stop:
                    p.BeginPath();
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = (22.5f + i * 45) * Mathf.Deg2Rad; var point = P(.5f + Mathf.Cos(angle) * .4f, .5f + Mathf.Sin(angle) * .4f);
                        if (i == 0) p.MoveTo(point); else p.LineTo(point);
                    }
                    Solid(ink); Bar(.28f, .5f, .72f, .5f, .13f, Cut, false);
                    break;
                case RtsGlyph.Focus:
                    Curve(.5f, .5f, .27f, 0, 360, .085f);
                    Bar(.5f, .05f, .5f, .27f, .09f, ink); Bar(.5f, .73f, .5f, .95f, .09f, ink);
                    Bar(.05f, .5f, .27f, .5f, .09f, ink); Bar(.73f, .5f, .95f, .5f, .09f, ink);
                    Disc(.5f, .5f, .075f, ink);
                    break;
                case RtsGlyph.Board: Anchor(.38f, .8f); VerticalArrow(.84f, true, .14f, .62f); break;
                case RtsGlyph.Unload: Anchor(.38f, .8f); VerticalArrow(.84f, false, .38f, .86f); break;
                case RtsGlyph.Speaker:
                    Begin(.08f, .37f); To(.28f, .37f); To(.52f, .15f); To(.52f, .85f); To(.28f, .63f); To(.08f, .63f); Solid(ink);
                    if (!struck) { Curve(.52f, .5f, .18f, -48, 48, .08f); Curve(.52f, .5f, .34f, -48, 48, .08f); }
                    break;
                case RtsGlyph.Note:
                    Bar(.5f, .74f, .5f, .14f, .08f, ink);
                    Begin(.5f, .12f); To(.84f, .25f); To(.84f, .42f); To(.5f, .3f); Solid(ink);
                    Disc(.38f, .75f, .15f, ink);
                    break;
                case RtsGlyph.Ranking:
                    Rect(.1f, .52f, .22f, .36f, shade); Rect(.39f, .18f, .22f, .7f, ink); Rect(.68f, .36f, .22f, .52f, shade);
                    break;
                case RtsGlyph.Map:
                    Begin(.08f, .22f); To(.36f, .12f); To(.36f, .78f); To(.08f, .88f); Solid(ink);
                    Begin(.36f, .12f); To(.64f, .22f); To(.64f, .88f); To(.36f, .78f); Solid(shade);
                    Begin(.64f, .22f); To(.92f, .12f); To(.92f, .78f); To(.64f, .88f); Solid(ink);
                    Disc(.24f, .56f, .05f, Cut, false); Disc(.5f, .44f, .05f, Cut, false); Disc(.78f, .5f, .05f, Cut, false);
                    break;
                case RtsGlyph.Hand:
                    // Open palm: four fingers, a thumb to the left, then the palm over their roots.
                    Bar(.33f, .5f, .33f, .2f, .12f, ink); Bar(.47f, .48f, .47f, .1f, .12f, ink);
                    Bar(.61f, .48f, .61f, .14f, .12f, ink); Bar(.75f, .52f, .75f, .26f, .11f, ink);
                    Bar(.26f, .66f, .12f, .44f, .12f, ink);
                    Begin(.26f, .44f); To(.81f, .44f); To(.79f, .74f); To(.64f, .92f); To(.36f, .92f); To(.24f, .72f); Solid(ink);
                    break;
                case RtsGlyph.Chat:
                    Begin(.12f, .16f); To(.88f, .16f); To(.88f, .66f); To(.46f, .66f); To(.24f, .88f); To(.28f, .66f); To(.12f, .66f); Solid(ink);
                    Disc(.32f, .41f, .06f, Cut, false); Disc(.5f, .41f, .06f, Cut, false); Disc(.68f, .41f, .06f, Cut, false);
                    break;
                case RtsGlyph.Close: Bar(.2f, .2f, .8f, .8f, .15f, ink); Bar(.8f, .2f, .2f, .8f, .15f, ink); break;
                case RtsGlyph.Queue:
                    Disc(.14f, .84f, .06f, ink); Disc(.3f, .72f, .06f, ink); Disc(.46f, .64f, .06f, ink);
                    Bar(.68f, .66f, .68f, .12f, .07f, ink);
                    Begin(.68f, .1f); To(.94f, .22f); To(.68f, .36f); Solid(ink);
                    Disc(.68f, .68f, .06f, ink);
                    break;
                case RtsGlyph.ChevronUp: Chevron(true); break;
                case RtsGlyph.ChevronDown: Chevron(false); break;
            }
            if (struck) Bar(.12f, .88f, .88f, .12f, .1f, Strike);
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
            // HUD buttons all have hotkeys: a focused button would re-fire on Enter/Space
            // while the same keypress also reaches the game's global shortcuts.
            focusable=false;
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

    /// <summary>A quiet campaign-screen divider that keeps the menu distinct from framed battle chrome.</summary>
    public sealed class RtsCampaignDivider : VisualElement
    {
        public RtsCampaignDivider()
        {
            name="Campaign divider";pickingMode=PickingMode.Ignore;
            style.height=12;style.marginTop=5;style.flexShrink=0;
            generateVisualContent+=Paint;
        }

        void Paint(MeshGenerationContext context)
        {
            var r=contentRect;if(r.width<24)return;
            var p=context.painter2D;float y=r.height*.5f;
            var edge=new Color(RtsUiStyle.Bronze.r,RtsUiStyle.Bronze.g,RtsUiStyle.Bronze.b,.42f);
            RtsOrnamentDrawing.Line(p,new Vector2(0,y),new Vector2(r.width*.46f,y),edge,1);
            RtsOrnamentDrawing.Line(p,new Vector2(r.width*.54f,y),new Vector2(r.width,y),edge,1);
            RtsOrnamentDrawing.Diamond(p,new Vector2(r.width*.5f,y),4,RtsUiStyle.Gold);
        }
    }

    /// <summary>Round-timer dial beside the gold: fills clockwise until the next income.</summary>
    public sealed class RtsIncomeRing : VisualElement
    {
        float progress;
        public RtsIncomeRing()
        {
            name="HUD income ring";pickingMode=PickingMode.Ignore;
            style.width=18;style.height=18;style.flexShrink=0;
            generateVisualContent+=Paint;
        }
        public float Progress
        {
            get=>progress;
            set { value=Mathf.Clamp01(value); if(Mathf.Abs(value-progress)<.004f)return; progress=value; MarkDirtyRepaint(); }
        }
        void Paint(MeshGenerationContext context)
        {
            var p=context.painter2D;var r=contentRect;
            float radius=Mathf.Min(r.width,r.height)*.5f-2;if(radius<=1)return;
            p.lineWidth=3;p.lineCap=LineCap.Butt;
            p.strokeColor=new Color(.2f,.17f,.1f,1);p.BeginPath();p.Arc(r.center,radius,Angle.Degrees(0),Angle.Degrees(359.9f));p.Stroke();
            if(progress<=.001f)return;
            p.strokeColor=RtsUiStyle.Gold;p.BeginPath();
            p.Arc(r.center,radius,Angle.Degrees(-90),Angle.Degrees(-90+Mathf.Min(359.9f,360*progress)));p.Stroke();
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
