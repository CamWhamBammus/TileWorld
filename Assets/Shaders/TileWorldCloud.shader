// Clouds. Two things in one shader: the lumps of a cumulus (mode 0), lit
// by the sun on the faces that meet it and dark underneath, with a silver
// lining when the sun is behind them; and a sheet (mode 1), a ceiling
// whose cover is a noise in world space thresholded by how overcast it is,
// lighter and darker by its thickness. Neither takes the fog -- they are
// too far off for it -- but both melt toward the haze colour with distance
// and fade out altogether toward the horizon.
Shader "TileWorld/Cloud"
{
    Properties
    {
        _Color ("Lit", Color) = (1, 1, 1, 1)
        _Shadow ("Shade", Color) = (0.62, 0.66, 0.74, 1)
        _Mode ("Mode (0 lumps, 1 sheet)", Float) = 0
        _Coverage ("Coverage", Range(0, 1)) = 0.5
        _Scale ("Noise scale", Float) = 0.0012
        _Drift ("Drift", Vector) = (0, 0, 0, 0)
        _FadeStart ("Fade start", Float) = 900
        _FadeEnd ("Fade end", Float) = 1500
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Stretch ("Stretch", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+40" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Cloud"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color, _Shadow, _Drift;
            float _Mode, _Coverage, _Scale, _FadeStart, _FadeEnd, _Alpha, _Stretch;
            CBUFFER_END

            float4 _MoonDir;
            float4 _MoonColor;
            // the sun as the clouds see it: still lit when the ground has lost it
            float4 _CloudSun;
            float4 _CloudSunDir;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int o = 0; o < 4; o++) { v += a * Noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 view = normalize(GetCameraPositionWS() - i.positionWS);
                float dist = distance(GetCameraPositionWS(), i.positionWS);
                float3 ambient = SampleSH(float3(0, 1, 0));
                float3 moon = _MoonColor.rgb * 0.5;
                float3 sunColor = _CloudSun.rgb;
                float3 sunDir = _CloudSunDir.xyz;
                float low = 1 - saturate(sunDir.y * 5);          // the sun near the horizon
                float3 col;
                float alpha;

                if (_Mode < 0.5)
                {
                    float3 n = normalize(i.normalWS);
                    float ndl = dot(n, sunDir);
                    // lit where the sun meets it, darker underneath -- except at sunset, when the
                    // light comes in under them and the whole cloud takes the colour
                    float3 light = ambient * 0.8 + sunColor * (0.3 + 0.7 * saturate(ndl)) + moon * (0.4 + 0.6 * saturate(dot(n, _MoonDir.xyz)));
                    float under = lerp(0.55, 1, saturate(n.y * 0.6 + 0.5));
                    under = lerp(under, 1, low * 0.8);
                    col = lerp(_Shadow.rgb, _Color.rgb, under) * light;
                    col += sunColor * low * 0.45;
                    // the silver lining: the edge, with the sun behind
                    float rim = pow(1 - saturate(abs(dot(n, view))), 3) * saturate(dot(-view, sunDir)) * 1.4;
                    col += sunColor * rim;
                    alpha = _Alpha;
                }
                else
                {
                    float2 p = (i.positionWS.xz + _Drift.xy) * _Scale;
                    p.x *= _Stretch;
                    float cover = Fbm(p);
                    float edge = 1 - _Coverage;
                    alpha = smoothstep(edge - 0.22, edge + 0.08, cover) * _Alpha;
                    float thick = saturate((cover - edge) / max(0.05, 1 - edge));
                    // thin cloud is bright, thick cloud is shaded underneath, and the underside is mottled
                    float mottle = Noise(p * 3.7 + _Drift.xy * 0.0003) * 0.36 + 0.82;
                    float3 light = ambient * 0.9 + sunColor * lerp(0.8, 0.28, thick) + moon * 0.5;
                    col = lerp(_Color.rgb, _Shadow.rgb, thick * 0.9) * light * mottle;
                    col += sunColor * low * 0.25 * (1 - thick);
                }

                // distance: toward the haze, then gone at the horizon
                float haze = saturate((dist - 250) / 1400);
                col = lerp(col, unity_FogColor.rgb, haze * 0.7);
                alpha *= 1 - smoothstep(_FadeStart, _FadeEnd, dist);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
