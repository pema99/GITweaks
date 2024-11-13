Shader "Unlit/RenderUVMask"
{
    Properties
    {
    }
    SubShader
    {
        Pass
        {
            Conservative True
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ USE_UV1

            struct appdata
            {
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            float4 _CandidateST;
            int _CandidateIndex;
            
            v2f vert (appdata v)
            {
                v2f o;
                float2 uv = v.uv * _CandidateST.xy + _CandidateST.zw;
                o.vertex = float4(float2(1,-1)*(uv*2-1),0,1);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return _CandidateIndex;
            }
            ENDCG
        }
    }
}
