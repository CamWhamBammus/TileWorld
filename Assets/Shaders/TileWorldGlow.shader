// Things that give light rather than take it: the moon, a shaft of sun
// between trees, a lantern's glow. Unlit, added onto what is behind, no fog
// (a moon nine hundred metres off would otherwise be fogged to nothing),
// and the vertex colour's alpha fades a shape out along itself.
Shader "TileWorld/Glow"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Strength ("Strength", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Strength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // soft toward the sides of a quad, so a beam or a disc has no hard edge
                float edge = 1 - abs(i.uv.x * 2 - 1);
                edge = edge * edge * (3 - 2 * edge);
                float a = i.color.a * _Color.a * edge;
                return half4(_Color.rgb * i.color.rgb * _Strength, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
