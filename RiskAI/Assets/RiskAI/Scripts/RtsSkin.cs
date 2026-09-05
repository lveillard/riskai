using System.Collections.Generic;
using UnityEngine;

namespace RiskAI
{
    public static class RtsSkin
    {
        public static GUIStyle Text, Small, Title, Center, Button, Tiny;
        public static readonly Color Gold = new Color(1, .94f, .4f), Muted = new Color(.89f,.88f,.77f);
        static Texture2D stone, card, hover, pressed, paintedStone;
        static bool ready;
        public static void Initialize()
        {
            if(ready)return;ready=true;
            paintedStone=Resources.Load<Texture2D>("Painted/ArchitectureAtlas");
            stone=Texture(new Color(.085f,.083f,.076f),true);card=Texture(new Color(.14f,.13f,.105f),true);
            hover=Texture(new Color(.35f,.33f,.23f),false);pressed=Texture(new Color(.45f,.38f,.2f),false);
            Text=new GUIStyle(GUI.skin.label){fontSize=14,normal={textColor=new Color(.95f,.94f,.85f)}};
            Small=new GUIStyle(Text){fontSize=13,normal={textColor=Muted}};
            Tiny=new GUIStyle(Small){fontSize=12};
            Title=new GUIStyle(Text){font=Font.CreateDynamicFontFromOSFont(new[]{"Georgia","Times New Roman"},20),fontSize=20,fontStyle=FontStyle.Bold,normal={textColor=Gold}};
            Center=new GUIStyle(Text){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
            Button=new GUIStyle(GUI.skin.button){fontSize=13,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(9,6,4,4),border=new RectOffset(1,1,1,1)};
            Button.normal.background=card;Button.hover.background=hover;Button.active.background=pressed;
            Button.normal.textColor=Button.hover.textColor=Button.active.textColor=new Color(.95f,.94f,.83f);
            Button.onNormal.background=pressed;Button.onHover.background=hover;Button.onActive.background=pressed;
            Button.onNormal.textColor=Button.onHover.textColor=Button.onActive.textColor=Gold;
        }
        static Texture2D Texture(Color color,bool grain)
        {
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            var random=new System.Random(84);var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float shade=grain?.88f+(float)random.NextDouble()*.2f:1;
                if(y==0||x==0)shade+=.13f;if(y==63||x==63)shade-=.2f;
                pixels[y*64+x]=new Color(color.r*shade,color.g*shade,color.b*shade,1);
            }
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        public static void Fill(Rect r,Color color)
        { Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old; }
        public static void Frame(Rect r,Color? trim=null)
        {
            GUI.DrawTexture(r,stone,ScaleMode.StretchToFill);Color edge=trim??new Color(.51f,.42f,.25f);
            if(paintedStone&&r.width>1100)
            {
                // Timber rails and heavy iron straps frame the playable view.
                for(float x=r.x;x<r.xMax;x+=96)
                {
                    float w=Mathf.Min(96,r.xMax-x);
                    var rail=new Rect(x,r.y-4,w,14);
                    GUI.DrawTextureWithTexCoords(rail,paintedStone,new Rect(.025f,.13f,.25f,.045f));
                    GUI.DrawTextureWithTexCoords(new Rect(x,r.yMax-10,w,10),paintedStone,new Rect(.03f,.23f,.25f,.04f));
                    Fill(new Rect(x,rail.yMax-2,w,2),new Color(.025f,.02f,.015f));
                }
                if(r.height>100)foreach(float x in new[]{r.x+4,r.x+230,r.xMax-667,r.xMax-12})
                {
                    GUI.DrawTextureWithTexCoords(new Rect(x,r.y,9,r.height),paintedStone,new Rect(.035f,.025f,.038f,.43f));
                    var cap=new Rect(x-6,r.y-7,21,22);Fill(cap,new Color(.11f,.12f,.12f));
                    Fill(new Rect(cap.x,cap.y,cap.width,2),new Color(.43f,.45f,.44f));
                    Fill(new Rect(x,r.y-1,4,4),new Color(.62f,.62f,.55f));
                    Fill(new Rect(x,r.y+10,4,4),new Color(.35f,.36f,.34f));
                }
            }
            Fill(new Rect(r.x,r.y,r.width,2),edge);Fill(new Rect(r.x,r.y,2,r.height),edge);
            Fill(new Rect(r.x,r.yMax-2,r.width,2),new Color(.025f,.03f,.025f));Fill(new Rect(r.xMax-2,r.y,2,r.height),new Color(.025f,.03f,.025f));
            foreach(float x in new[]{r.x+5,r.xMax-8})foreach(float y in new[]{r.y+5,r.yMax-8})Fill(new Rect(x,y,3,3),edge);
        }
        public static void Bar(Rect r,float amount,Color color)
        { Fill(r,new Color(.025f,.03f,.025f));Fill(new Rect(r.x+1,r.y+1,(r.width-2)*Mathf.Clamp01(amount),r.height-2),color); }
    }
}
