// Ícone de alerta dos riscos (anel + "!" + seta), transparente normal (não estoura no fundo claro).
// Textura: R = quanto o pixel vai para o branco (núcleo), A = transparência. A cor vem de _BaseColor,
// que o IndicadorRisco troca entre a cor pendente e a cor de mira. Compatível com estéreo do Quest.
Shader "BB Seguros/Indicador Alerta"
{
    Properties
    {
        _BaseMap ("Textura (R núcleo, A alfa)", 2D) = "white" {}
        _BaseColor ("Cor", Color) = (1, 1, 1, 1)
        _Opacidade ("Opacidade", Range(0, 1)) = 1
        _Branco ("Núcleo branco", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
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
                float _Opacidade;
                float _Branco;
            CBUFFER_END

            struct Atributos
            {
                float4 posicao : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Variaveis
            {
                float4 posicao : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Variaveis vert(Atributos i)
            {
                Variaveis o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.posicao = TransformObjectToHClip(i.posicao.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(Variaveis i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 cor = lerp(saturate(_BaseColor.rgb), half3(1, 1, 1), t.r * _Branco);
                return half4(cor, saturate(t.a * _BaseColor.a * _Opacidade));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
