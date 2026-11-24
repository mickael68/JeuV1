// Roche des grottes : texture projetée sur les 3 axes du monde (pas d'étirement, pas d'UV),
// sol et paroi mélangés selon l'inclinaison de la surface.
Shader "TheEchoesBelow/Cave Triplanar"
{
    Properties
    {
        _WallMap ("Paroi", 2D) = "white" {}
        [Normal] _WallNormal ("Paroi (normale)", 2D) = "bump" {}
        _WallColor ("Teinte paroi", Color) = (1, 1, 1, 1)
        _WallScale ("Taille du motif paroi (m)", Float) = 6
        _FloorMap ("Sol", 2D) = "white" {}
        [Normal] _FloorNormal ("Sol (normale)", 2D) = "bump" {}
        _FloorColor ("Teinte sol", Color) = (1, 1, 1, 1)
        _FloorScale ("Taille du motif sol (m)", Float) = 4
        _FloorStart ("Sol à partir de (pente)", Range(0, 1)) = 0.6
        _FloorBlend ("Transition sol / paroi", Range(0.01, 0.5)) = 0.18
        _NormalStrength ("Relief", Range(0, 2)) = 1.2
        _Smoothness ("Brillance", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _WallColor, _FloorColor;
            float _WallScale, _FloorScale, _FloorStart, _FloorBlend, _NormalStrength, _Smoothness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #define _ENVIRONMENTREFLECTIONS_OFF 1   // sous terre : pas de reflet du ciel
            #define _NORMALMAP 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_WallMap);     SAMPLER(sampler_WallMap);
            TEXTURE2D(_WallNormal);  SAMPLER(sampler_WallNormal);
            TEXTURE2D(_FloorMap);    SAMPLER(sampler_FloorMap);
            TEXTURE2D(_FloorNormal); SAMPLER(sampler_FloorNormal);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half fogFactor    : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Projection triplanaire (couleur + normale, mélange "whiteout")
            void Triplanar(TEXTURE2D_PARAM(albedoTex, albedoSmp), TEXTURE2D_PARAM(normalTex, normalSmp),
                           float3 p, float3 n, float3 w, float scale, out half3 albedo, out float3 normalWS)
            {
                float3 sgn = float3(n.x < 0 ? -1 : 1, n.y < 0 ? -1 : 1, n.z < 0 ? -1 : 1);
                float2 uvX = p.zy / scale, uvY = p.xz / scale, uvZ = p.xy / scale;
                uvX.x *= sgn.x; uvY.x *= sgn.y; uvZ.x *= -sgn.z;

                albedo = SAMPLE_TEXTURE2D(albedoTex, albedoSmp, uvX).rgb * w.x
                       + SAMPLE_TEXTURE2D(albedoTex, albedoSmp, uvY).rgb * w.y
                       + SAMPLE_TEXTURE2D(albedoTex, albedoSmp, uvZ).rgb * w.z;

                half3 tX = UnpackNormalScale(SAMPLE_TEXTURE2D(normalTex, normalSmp, uvX), _NormalStrength);
                half3 tY = UnpackNormalScale(SAMPLE_TEXTURE2D(normalTex, normalSmp, uvY), _NormalStrength);
                half3 tZ = UnpackNormalScale(SAMPLE_TEXTURE2D(normalTex, normalSmp, uvZ), _NormalStrength);
                tX.x *= sgn.x; tY.x *= sgn.y; tZ.x *= -sgn.z;
                tX = half3(tX.xy + n.zy, abs(tX.z) * n.x);
                tY = half3(tY.xy + n.xz, abs(tY.z) * n.y);
                tZ = half3(tZ.xy + n.xy, abs(tZ.z) * n.z);
                normalWS = normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 w = pow(abs(n), 4.0);
                w /= (w.x + w.y + w.z);

                half3 wallAlb, floorAlb; float3 wallN, floorN;
                Triplanar(TEXTURE2D_ARGS(_WallMap, sampler_WallMap), TEXTURE2D_ARGS(_WallNormal, sampler_WallNormal), i.positionWS, n, w, _WallScale, wallAlb, wallN);
                Triplanar(TEXTURE2D_ARGS(_FloorMap, sampler_FloorMap), TEXTURE2D_ARGS(_FloorNormal, sampler_FloorNormal), i.positionWS, n, w, _FloorScale, floorAlb, floorN);

                // Le sol gagne sur les surfaces plates ; la luminance de la roche casse la ligne de transition
                half lum = dot(wallAlb, half3(0.3, 0.59, 0.11));
                half f = smoothstep(_FloorStart - _FloorBlend, _FloorStart + _FloorBlend, n.y + (lum - 0.35) * 0.35);

                SurfaceData s = (SurfaceData)0;
                s.albedo = lerp(wallAlb * _WallColor.rgb, floorAlb * _FloorColor.rgb, f);
                s.alpha = 1;
                s.metallic = 0;
                s.smoothness = _Smoothness;
                s.occlusion = 1;
                s.normalTS = half3(0, 0, 1);

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = normalize(lerp(wallN, floorN, f));
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fogFactor;
                d.bakedGI = SampleSH(d.normalWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);

                half4 c = UniversalFragmentPBR(d, s);
                c.rgb = MixFog(c.rgb, d.fogCoord);
                c.a = 1;
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

            float4 ShadowVert(A v) : SV_POSITION
            {
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - p);
            #else
                float3 l = _LightDirection;
            #endif
                float4 c = TransformWorldToHClip(ApplyShadowBias(p, n, l));
            #if UNITY_REVERSED_Z
                c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
            #else
                c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return c;
            }
            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull Back

            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V DNVert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 DNFrag(V i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
