Shader "Hidden/pema99/Overlay"
{
    Properties
    {
        [MainColor] _Color("Color", Color) = (0,0,0,1)
        [MainTexture] _MainTex("MainTex", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" }

        Pass
        {
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _Color;
            sampler2D _MainTex;
            float _Alpha;
            int _Mode;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                if (_Mode == 2)
                {
                    return float4(tex2D(_MainTex, i.uv).rgb, 1);
                }
                else if (_Mode == 1)
                {
                    return tex2D(_MainTex, i.uv).a * _Alpha;
                }
                else
                {
                    return _Color;
                }
            }
            ENDCG
        }
    }
}
