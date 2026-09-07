// The wash on a beach: a wave runs up the sand and slides back, over and
// over, on its own clock. The mesh is quads over the strand and the
// shallows, each vertex carrying how far it is from the waterline (uv.x,
// metres, negative in the water) and a phase (uv.y) so the coast does not
// move as one. The front of the wave is a band of foam, brightest coming
// in; behind it a thin sheet of water lies on the sand and thins as the
// wave draws back, leaving lines of foam.
Shader "TileWorld/Wash"
{
    Properties
    {
        _Foam ("Foam", Color) = (0.96, 0.98, 1, 1)
        _Film ("Film", Color) = (0.62, 0.80, 0.86, 1)
        _Period ("Period", Float) = 7.5
        _Low ("Lowest reach", Float) = -4
        _High ("Highest reach", Float) = 6.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Wash"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Foam, _Film;
            float _Period, _Low, _High;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; float fog : TEXCOORD2; };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float dist = i.uv.x;
                float cycle = frac(_Time.y / _Period + i.uv.y);
                // in fast, out slow
                float f = cycle < 0.36 ? smoothstep(0, 1, cycle / 0.36) : 1 - smoothstep(0, 1, (cycle - 0.36) / 0.64);
                float front = lerp(_Low, _High, f);
                float coming = cycle < 0.36 ? 1 : 0;
                float t = _Time.y;

                // the foam at the front, broken up along the shore: this is the wave
                float breakup = Noise(i.positionWS.xz * 1.8 + float2(t * 0.4, -t * 0.3)) * 0.8 + 0.4;
                // squared by hand: pow of a negative base is NaN on the GPU, and behind the front it is negative
                float off = (dist - front) / 1.2;
                float band = exp(-off * off);
                float foam = band * lerp(0.65, 1, coming) * breakup;

                // a thin sheet of water just behind the front, gone a few metres back
                float sheet = (dist < front) * saturate(1 - (front - dist) / 3.5) * 0.16;
                sheet *= dist > -0.5 ? 1 : 0.5;

                // faint lines of foam left on the sand as the wave draws back
                float lines = smoothstep(0.85, 1, frac(dist * 0.9 + Noise(i.positionWS.xz * 0.6) * 0.5)) * (dist > 0 && dist < front) * (1 - coming) * 0.22 * breakup;

                float a = saturate(foam + sheet + lines);
                float3 col = lerp(_Film.rgb, _Foam.rgb, saturate((foam + lines) / max(a, 0.001)));
                col = MixFog(col, i.fog);
                return half4(col, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
