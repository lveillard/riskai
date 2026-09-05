using UnityEngine;

namespace RiskAI
{
    public static class RtsCursor
    {
        static Texture2D attack;
        static bool attacking;
        public static void SetAttack(bool value)
        {
            if(attacking==value)return;attacking=value;
            if(!value){Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);return;}
            if(!attack)
            {
                attack=new Texture2D(32,32,TextureFormat.RGBA32,false);var pixels=new Color[1024];
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                {
                    int dx=Mathf.Abs(x-15),dy=Mathf.Abs(y-15);
                    bool line=(dx<=1&&dy>=5&&dy<=13)||(dy<=1&&dx>=5&&dx<=13);
                    bool border=(dx<=2&&dy>=4&&dy<=14)||(dy<=2&&dx>=4&&dx<=14);
                    pixels[y*32+x]=line?new Color(1,.22f,.1f,1):border?Color.black:Color.clear;
                }
                attack.SetPixels(pixels);attack.Apply();
            }
            Cursor.SetCursor(attack,new Vector2(15,15),CursorMode.Auto);
        }
    }
}
