Shader "FogOfWar/ProjectedFog"
{
    Properties
    {
        _FogTex ("Fog Texture", 2D) = "white" {}
        _DepthTex ("Depth Texture", 2D) = "black" {}
        _ChunkSize ("Chunk Size", Vector) = (1,1,0,0)
        _WorldOrigin ("World Origin", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FogTex;
            sampler2D _DepthTex;

            float4 _ChunkSize;
            float4 _WorldOrigin;
            float4x4 _InvViewProjMatrix;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Convert screen-space position to world position
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float depth = tex2Dproj(_DepthTex, UNITY_PROJ_COORD(i.screenPos)).r;

                // Reconstruct world position
                float4 ndcPos = float4(uv * 2 - 1, depth, 1.0);
                float4 worldPos = mul(_InvViewProjMatrix, ndcPos);
                worldPos /= worldPos.w;

                // Convert world position to 2D texture space
                float2 worldUV = (worldPos.xz - _WorldOrigin.xz) / _ChunkSize.xy;

                // Sample fog texture
                fixed4 fogColor = tex2D(_FogTex, worldUV);

                return fogColor;
            }
            ENDCG
        }
    }
}
