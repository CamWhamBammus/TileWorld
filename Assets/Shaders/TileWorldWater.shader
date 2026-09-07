// Tile World's water. Colour by depth, the bed seen through it and bent a
// little, the sun glinting off a surface that moves, foam where it meets
// the shore or anything standing in it, and the sky in it at a low angle.
// Everything is worked from world position and the clock: the water mesh is
// bare quads with neither UVs nor normals.
Shader "TileWorld/Water"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.30, 0.58, 0.56, 0.32)
        _Deep ("Deep", Color) = (0.05, 0.20, 0.34, 0.94)
        _Foam ("Foam", Color) = (0.93, 0.97, 0.98, 1)
        _DepthFade ("Depth to full colour", Float) = 2.4
        _FoamDepth ("Foam depth", Float) = 0.22
        _WaveHeight ("Wave height", Float) = 0.035
        _WaveScale ("Wave scale", Float) = 0.45
        _Speed ("Speed", Float) = 0.6
        _Sparkle ("Sparkle", Float) = 0.7
        _Refract ("Refraction", Float) = 0.04
        _Fresnel ("Sky at the edge", Float) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Shallow, _Deep, _Foam;
            float _DepthFade, _FoamDepth, _WaveHeight, _WaveScale, _Speed, _Sparkle, _Refract, _Fresnel;
            CBUFFER_END

            // the moon, set by the clock: where it is, and how bright
            float4 _MoonDir;
            float4 _MoonColor;

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            // two crossing waves and a slower swell, all from world position
            float Height(float2 p, float t)
            {
                float a = sin(p.x * 0.9 * _WaveScale + t * 1.1) * sin(p.y * 1.3 * _WaveScale - t * 0.8);
                float b = sin((p.x + p.y) * 0.5 * _WaveScale + t * 0.6);
                return (a * 0.6 + b * 0.4) * _WaveHeight;
            }

            float3 Normal(float2 p, float t)
            {
                float e = 0.15;
                float h0 = Height(p, t);
                float hx = Height(p + float2(e, 0), t);
                float hz = Height(p + float2(0, e), t);
                // ripples finer than the waves, for the glints
                float rx = cos(p.x * 6.1 + t * 2.3) * sin(p.y * 4.7 - t * 1.9) * 0.02;
                float rz = sin(p.x * 5.3 - t * 2.1) * cos(p.y * 6.7 + t * 1.7) * 0.02;
                return normalize(float3(-(hx - h0) / e - rx, 1, -(hz - h0) / e - rz));
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                ws.y += Height(ws.xz, _Time.y * _Speed);
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float3 n = Normal(i.positionWS.xz, t);
                float3 view = normalize(GetCameraPositionWS() - i.positionWS);

                // how much water is under this point of the surface, along the view
                float surfaceDepth = i.positionCS.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                // along the view ray, then made vertical: at a low angle a
                // ray runs a long way through shallow water
                float depth = max(0, sceneDepth - surfaceDepth) * max(0.08, abs(view.y));

                // the bed, bent by the surface; not bent where that would drag in something above the water
                float2 bent = uv + n.xz * _Refract * saturate(depth);
                float bentDepth = LinearEyeDepth(SampleSceneDepth(bent), _ZBufferParams);
                if (bentDepth < surfaceDepth) bent = uv;
                float3 bed = SampleSceneColor(bent);

                float fade = saturate(depth / _DepthFade);
                float4 tint = lerp(_Shallow, _Deep, fade);
                float3 water = lerp(bed, tint.rgb, tint.a);

                // foam at the shore and round anything in it, breaking up as it goes
                float foamNoise = sin(i.positionWS.x * 7.3 + t * 2.0) * cos(i.positionWS.z * 6.1 - t * 1.6) * 0.5 + 0.5;
                float foam = saturate(1 - depth / _FoamDepth) * smoothstep(0.5, 0.75, foamNoise) * 0.7;
                water = lerp(water, _Foam.rgb, foam);

                // the sun on it
                Light sun = GetMainLight();
                float3 h = normalize(sun.direction + view);
                float glint = pow(saturate(dot(n, h)), 140) * _Sparkle;
                float soft = pow(saturate(dot(n, h)), 12) * 0.08;
                water += sun.color * (glint + soft);

                // and the moon on it, when there is one
                float3 hm = normalize(_MoonDir.xyz + view);
                float moonGlint = pow(saturate(dot(n, hm)), 160) * _Sparkle * 1.4 + pow(saturate(dot(n, hm)), 10) * 0.05;
                water += _MoonColor.rgb * moonGlint;

                // the sky in it at a low angle: the fog's colour stands for the sky
                float fresnel = pow(1 - saturate(dot(view, n)), 3) * _Fresnel;
                water = lerp(water, unity_FogColor.rgb, fresnel * (0.4 + 0.6 * fade));

                water = MixFog(water, i.fogFactor);
                return half4(water, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
