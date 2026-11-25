// Eau des sous-sols : surface animée (vagues calculées, sans texture), courant orienté selon l'objet,
// transparence et couleur selon la profondeur, réfraction de ce qu'il y a dessous, reflets des lampes,
// liseré d'écume au contact de la roche.
// Demande "Depth Texture" et "Opaque Texture" dans l'asset URP (activés par le générateur).
Shader "TheEchoesBelow/Water"
{
    Properties
    {
        _ShallowColor ("Teinte peu profonde", Color) = (0.55, 0.65, 0.65, 1)
        _DeepColor ("Couleur profonde", Color) = (0.01, 0.03, 0.04, 1)
        _DepthMax ("Profondeur où l'eau devient opaque (m)", Float) = 1.2
        _WaveScale ("Taille des vagues (m)", Float) = 0.6
        _WaveStrength ("Force des vagues", Range(0, 1)) = 0.35
        _WaveSpeed ("Vitesse des vagues", Float) = 0.4
        _FlowSpeed ("Courant (m/s, le long de +Z de l'objet)", Float) = 0
        _Refraction ("Réfraction", Range(0, 0.1)) = 0.03
        _Smoothness ("Brillance", Range(0, 1)) = 0.93
        _FoamColor ("Écume", Color) = (0.6, 0.62, 0.6, 1)
        _FoamWidth ("Largeur de l'écume (m)", Float) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Blend Off
            Cull Off   // la surface se voit aussi depuis dessous (sous l'eau)

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #define _ENVIRONMENTREFLECTIONS_OFF 1   // sous terre : pas de reflet du ciel
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor;
                float _DepthMax, _WaveScale, _WaveStrength, _WaveSpeed, _FlowSpeed, _Refraction, _Smoothness, _FoamWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                float2 flowDir    : TEXCOORD2;
                half fogFactor    : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                float3 f = mul((float3x3)UNITY_MATRIX_M, float3(0, 0, 1));
                o.flowDir = normalize(f.xz + 1e-5);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Pente d'une somme de vagues orientées (dérivées analytiques) → normale
            static const float2 WaveDirs[6] = { float2(0.98, 0.17), float2(-0.43, 0.9), float2(0.6, -0.8), float2(-0.87, -0.5), float2(0.26, 0.97), float2(-0.99, 0.12) };
            static const float WaveFreqs[6] = { 1.0, 1.37, 1.93, 2.71, 3.6, 4.9 };

            float2 WaveSlope(float2 p, float t)
            {
                float2 slope = 0;
                [unroll] for (int i = 0; i < 6; i++)
                {
                    float k = WaveFreqs[i] / _WaveScale;
                    float amp = 0.12 / WaveFreqs[i];
                    float phase = dot(WaveDirs[i], p) * k + t * _WaveSpeed * (1.3 + i * 0.37);
                    slope += WaveDirs[i] * (amp * k * cos(phase));
                }
                return slope * _WaveStrength;
            }

            half4 Frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float t = _Time.y;
                float2 p = i.positionWS.xz - i.flowDir * (_FlowSpeed * t);
                float2 slope = WaveSlope(p, t);
                float3 n = normalize(float3(-slope.x, 1, -slope.y));

                // Profondeur d'eau vue à travers ce pixel
                float2 uv = i.screenPos.xy / i.screenPos.w;

                // Vue depuis dessous : le monde au-dessus, déformé et assombri par l'eau
                if (!front)
                {
                    float2 uvU = uv + n.xz * _Refraction * 2.5;
                    half3 above = SampleSceneColor(uvU);
                    float fade = saturate(i.screenPos.w / 8.0);
                    half3 cU = lerp(above * _ShallowColor.rgb * 0.7, _DeepColor.rgb * 2.0, 0.35 + 0.55 * fade);
                    return half4(MixFog(cU, i.fogFactor), 1);
                }
                float surfDepth = i.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float depth = max(0, sceneDepth - surfDepth);

                // Réfraction : on décale la lecture du fond, sauf si on tomberait sur un objet devant l'eau
                float2 uvR = uv + n.xz * _Refraction * saturate(depth * 2);
                float depthR = LinearEyeDepth(SampleSceneDepth(uvR), _ZBufferParams) - surfDepth;
                if (depthR < 0) { uvR = uv; depthR = depth; }
                half3 below = SampleSceneColor(uvR);

                float k = saturate(depthR / max(0.01, _DepthMax));
                half3 water = lerp(below * _ShallowColor.rgb, _DeepColor.rgb, k * k * (3 - 2 * k));

                // Écume au contact (bord peu profond), un peu irrégulière
                float foamNoise = 0.5 + 0.5 * sin(p.x * 9.1 + t * 1.3) * sin(p.y * 7.7 - t * 0.9);
                float foam = saturate(1 - depth / max(0.001, _FoamWidth)) * foamNoise;

                // Éclairage : reflets spéculaires des lampes sur une surface très lisse
                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = n;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fogFactor;
                d.bakedGI = SampleSH(n) * 0.3;
                d.normalizedScreenSpaceUV = uv;
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = _FoamColor.rgb * foam;
                s.alpha = 1;
                s.smoothness = lerp(_Smoothness, 0.4, foam);
                s.occlusion = 1;
                s.normalTS = half3(0, 0, 1);
                half3 lit = UniversalFragmentPBR(d, s).rgb;

                half3 c = water + lit;
                c = MixFog(c, d.fogCoord);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
