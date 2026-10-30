// Partícula transparente que desenha por cima de qualquer objeto (ZTest Always).
// Usada no ícone de check do VFX de conserto. Textura x cor do material x cor da partícula. Compatível com estéreo do Quest.
Shader "AmbienteBB/Particula_SempreVisivel"
{
    Properties
    {
        _BaseMap ("Textura", 2D) = "white" {}
        [HDR] _BaseColor ("Cor", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+500" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            struct Atributos
            {
                float4 posicao : POSITION;
                float2 uv : TEXCOORD0;
                half4 cor : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Variaveis
            {
                float4 posicao : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 cor : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Variaveis vert(Atributos i)
            {
                Variaveis o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.posicao = TransformObjectToHClip(i.posicao.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.cor = i.cor;
                return o;
            }

            half4 frag(Variaveis i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor * i.cor;
            }
            ENDHLSL
        }
    }
}
