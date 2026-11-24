// Ciel panoramique qui fond deux HDRI l'un dans l'autre (transitions jour / nuit).
Shader "TheEchoesBelow/Skybox Blend"
{
    Properties
    {
        [NoScaleOffset] _TexA ("Ciel A (HDR)", 2D) = "grey" {}
        [NoScaleOffset] _TexB ("Ciel B (HDR)", 2D) = "grey" {}
        _ExposureA ("Exposition A", Float) = 1
        _ExposureB ("Exposition B", Float) = 1
        _Blend ("Mélange A → B", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _TexA, _TexB;
            half4 _TexA_HDR, _TexB_HDR;
            float _ExposureA, _ExposureB, _Blend;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            // Même projection que Skybox/Panoramic (latitude-longitude)
            float2 Radial(float3 d)
            {
                float3 n = normalize(d);
                float lat = acos(n.y);
                float lon = atan2(n.z, n.x);
                return float2(0.5, 1.0) - float2(lon, lat) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = Radial(i.dir);
                half3 a = DecodeHDR(tex2Dlod(_TexA, float4(uv, 0, 0)), _TexA_HDR) * _ExposureA;
                half3 b = DecodeHDR(tex2Dlod(_TexB, float4(uv, 0, 0)), _TexB_HDR) * _ExposureB;
                return half4(lerp(a, b, _Blend), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
