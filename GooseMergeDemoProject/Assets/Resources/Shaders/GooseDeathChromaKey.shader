Shader "Tuyoo/GooseDeathChromaKey"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _KeyColor ("Key Color", Color) = (0, 1, 0, 1)
        _Threshold ("Threshold", Range(0, 1)) = 0.05
        _Feather ("Feather", Range(0.001, 1)) = 0.22
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _KeyColor;
            float _Threshold;
            float _Feather;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                float greenDominance = col.g - max(col.r, col.b);
                float keyByDominance = smoothstep(_Threshold, _Threshold + _Feather, greenDominance);
                float keyByColor = 1.0 - smoothstep(0.12, 0.42, distance(col.rgb, _KeyColor.rgb));
                float key = max(keyByDominance, keyByColor);

                // Make the key edge decisive enough to remove the translucent green halo.
                float hardKey = smoothstep(0.10, 0.34, key);
                col.a *= saturate(1.0 - hardKey);
                col.rgb = lerp(
                    col.rgb,
                    float3(col.r, min(col.g, max(col.r, col.b)), col.b),
                    saturate(keyByDominance * 1.25));
                clip(col.a - 0.025);
                return col;
            }
            ENDCG
        }
    }
}
