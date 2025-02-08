Shader "FogOfWar/ProjectedFog"
{
    Properties
    {
        _FogTex ("Fog Texture", 2D) = "white" {}
        _DepthTex ("Depth Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _FogTex;
            sampler2D _DepthTex;
            float4x4 _InvViewProjMatrix;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float depthSample = tex2D(_DepthTex, screenUV).r;

                // Convert depth back to world position
                float4 ndcPos = float4(screenUV * 2 - 1, depthSample, 1.0);
                float4 worldPos = mul(_InvViewProjMatrix, ndcPos);
                worldPos /= worldPos.w;

                // Convert world position to fog texture UV
                float2 fogUV = worldPos.xz;
                fixed4 fogColor = tex2D(_FogTex, fogUV);

                return fogColor;
            }
            ENDCG
        }
    }
}
