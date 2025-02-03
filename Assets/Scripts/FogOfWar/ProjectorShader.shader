Shader "Custom/WorldProjector"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ProjectorMatrix ("Projector Matrix", Matrix) = "identity" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
            };

            uniform float4x4 _ProjectorMatrix;
            sampler2D _MainTex;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Project world position to texture coordinates
                o.uv = mul(_ProjectorMatrix, float4(v.vertex.xyz, 1)).xy;
                o.uv = o.uv * 0.5 + 0.5; // Transform into normalized texture space (0-1 range)
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}
