Shader "FogOfWar/ProjectedFog" {
    Properties {
        _MainTex ("Source Texture", 2D) = "white" {}
        _FogTex ("Fog Texture", 2D) = "white" {}
        _DepthTex ("Depth Texture", 2D) = "white" {}
        _BlurSize ("Depth Blur Size", Range(0.0, 3.0)) = 2.0
    }

    SubShader {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _FogTex;
            sampler2D _DepthTex;
            float4x4 _InvViewProjMatrix;
            float4x4 _ProjectorVP;
            float _BlurSize;

            v2f vert(appdata_t v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            // Depth precision issues near edges, we can just blur depthtex to fix it
            float SampleDepthBlurred(float2 uv) {
                float2 texelSize = _BlurSize / _ScreenParams.xy;

                float depth = 0;
                float total = 0;
                float kernel[9] = {
                    1, 2, 1,
                    2, 4, 2,
                    1, 2, 1
                };

                [unroll]
                for(int x = -1; x <= 1; x++) {
                    for(int y = -1; y <= 1; y++) {
                        float2 offset = float2(x, y) * texelSize;
                        float weight = kernel[(y + 1) * 3 + (x + 1)];
                        depth += SAMPLE_DEPTH_TEXTURE(_DepthTex, uv + offset) * weight;
                        total += weight;
                    }
                }

                return depth / total;
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 originalColor = tex2D(_MainTex, i.uv);

                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float depth = SampleDepthBlurred(screenUV);

                float4 ndcPos = float4(screenUV * 2 - 1, depth, 1);
                float4 worldPos = mul(_InvViewProjMatrix, ndcPos);
                worldPos.xyz /= worldPos.w;

                float4 projPos = mul(_ProjectorVP, float4(worldPos.xyz, 1.0));
                float3 projUV = projPos.xyz / projPos.w;

                if (abs(projUV.x) > 1 || abs(projUV.y) > 1 || projUV.z < 0 || projUV.z > 1) {
                    return originalColor;
                }

                float2 fogUV = (projUV.xy * 0.5 + 0.5);
                fixed4 fogColor = tex2D(_FogTex, fogUV);

                return lerp(originalColor, fogColor, fogColor.a);
            }
            ENDCG
        }
    }
}