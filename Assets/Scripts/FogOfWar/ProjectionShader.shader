Shader "FogOfWar/ProjectedFog"
{
    Properties
    {
        _MainTex ("Source Texture", 2D) = "white" {}
        _FogTex ("Fog Texture", 2D) = "white" {}
        _DepthTex ("Depth Texture", 2D) = "white" {}
        _WorldOrigin ("World Origin", Vector) = (0, 0, 0, 0)
        _GridSize ("Grid Size", Vector) = (10, 10, 0, 0)
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

            // Source texture from the camera
            sampler2D _MainTex;

            // Fog data
            sampler2D _FogTex;
            sampler2D _DepthTex;
            float4 _WorldOrigin;
            float4 _GridSize;
            float4x4 _InvViewProjMatrix;
            float4x4 _ProjectorVP;

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
                // Sample the original scene color
                fixed4 originalColor = tex2D(_MainTex, i.uv);

                // Get screen UV and depth
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float depth = SAMPLE_DEPTH_TEXTURE(_DepthTex, screenUV);
                float linearDepth = LinearEyeDepth(depth);

                // Reconstruct world position from depth
                float4 ndcPos = float4(screenUV * 2 - 1, depth, 1);
                float4 worldPos = mul(_InvViewProjMatrix, ndcPos);
                worldPos.xyz /= worldPos.w;

                // Transform to projector space
                float4 projPos = mul(_ProjectorVP, float4(worldPos.xyz, 1.0));

                // Check if point is within projector's frustum
                float3 projUV = projPos.xyz / projPos.w;
                if (abs(projUV.x) > 1 || abs(projUV.y) > 1 || projUV.z < 0 || projUV.z > 1)
                {
                    return originalColor; // Return the original scene color outside the frustum
                }

                // Convert to UV space and apply grid mapping
                float2 fogUV = (projUV.xy * 0.5 + 0.5);

                // Sample fog texture
                fixed4 fogColor = tex2D(_FogTex, fogUV);

                // Optional: Fade out fog at the edges of the projection
                float2 distFromCenter = abs(projUV.xy);
                float edgeFade = 1 - max(distFromCenter.x, distFromCenter.y);
                edgeFade = saturate(edgeFade * 10); // Adjust the multiplier to control fade sharpness
                fogColor.a *= edgeFade;

                // Blend fog with original scene
                return lerp(originalColor, fogColor, fogColor.a);
            }
            ENDCG
        }
    }
}