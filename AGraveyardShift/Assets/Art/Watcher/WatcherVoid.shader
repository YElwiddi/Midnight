// The watchers' skin: nothing lights it. Lamps, the torch and the moon leave it black, so in a beam it reads
// as a hole the shape of a man instead of the grey mannequin a lit black material turns into. It still takes
// distance fog, so far away it sinks into the night like everything else. It casts no shadow.
Shader "Custom/WatcherVoid"
{
    Properties
    {
        _Color ("Colour", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_FOG_COORDS(0)
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = fixed4(_Color.rgb, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
