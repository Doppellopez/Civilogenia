// Niebla de guerra: color y transparencia por vértice. El stencil hace que cada píxel se pinte una sola vez,
// así las casillas de niebla pueden solaparse (sin dejar huecos entre ellas) sin oscurecer dos veces el solape.
Shader "Civilogenia/Fog"
{
    SubShader
    {
        Tags { "Queue" = "Transparent+500" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Replace
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
