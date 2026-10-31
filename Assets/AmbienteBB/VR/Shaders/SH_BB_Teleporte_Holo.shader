// Holograma do ponto de teleporte, animado só no shader (sem script).
// UV1.x diz a parte da malha: 0 anel base (pulsa), 1 e 2 detalhes (giram em sentidos opostos),
// 3 feixes (faixa de luz subindo), 4 brilhinhos (sobem e somem). UV1.y é a fase de cada peça.
// Na malha, "para cima" é o -Z local. _BaseColor vem do PontoDeTeleporte (cor normal / mira);
// _Opacidade fica só no material e vale para todos os discos.
Shader "BB Seguros/Teleporte Holograma"
{
    Properties
    {
        _BaseMap ("Atlas", 2D) = "white" {}
        _BaseColor ("Cor", Color) = (1, 1, 1, 1)
        _Opacidade ("Opacidade", Range(0, 1)) = 1
        _Giro ("Giro dos detalhes (graus/s)", Float) = 14
        _Pulso ("Pulso do anel", Range(0, 0.5)) = 0.15
        _VelocidadeFeixe ("Velocidade da faixa nos feixes", Float) = 0.45
        _VelocidadeBrilho ("Velocidade dos brilhinhos", Float) = 0.22
        _AlturaBrilho ("Altura dos brilhinhos (m)", Float) = 0.6
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
                float _Giro;
                float _Pulso;
                float _VelocidadeFeixe;
                float _VelocidadeBrilho;
                float _AlturaBrilho;
            CBUFFER_END

            struct Atributos
            {
                float4 posicao : POSITION;
                float2 uv : TEXCOORD0;
                float2 parte : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Variaveis
            {
                float4 posicao : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 dados : TEXCOORD1; // tipo, fase, multiplicador de alfa
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 Girar(float2 p, float graus)
            {
                float s, c;
                sincos(radians(graus), s, c);
                return float2(p.x * c - p.y * s, p.x * s + p.y * c);
            }

            Variaveis vert(Atributos i)
            {
                Variaveis o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float tipo = i.parte.x;
                float fase = i.parte.y;
                float t = _Time.y;
                float3 p = i.posicao.xyz;
                float alfa = 1;

                if (tipo > 0.5 && tipo < 1.5) p.xy = Girar(p.xy, t * _Giro);
                else if (tipo > 1.5 && tipo < 2.5) p.xy = Girar(p.xy, -t * _Giro * 0.6);
                else if (tipo > 3.5)
                {
                    float ciclo = frac(t * _VelocidadeBrilho + fase);
                    p.z -= ciclo * _AlturaBrilho;
                    p.xy += float2(sin(t * 1.3 + fase * 6.283), cos(t * 1.1 + fase * 6.283)) * 0.012;
                    alfa = smoothstep(0.0, 0.15, ciclo) * (1 - smoothstep(0.55, 1.0, ciclo));
                }

                o.posicao = TransformObjectToHClip(p);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.dados = float3(tipo, fase, alfa);
                return o;
            }

            half4 frag(Variaveis i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 cor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float tipo = i.dados.x;
                float fase = i.dados.y;
                float t = _Time.y;
                float mult = i.dados.z;

                if (tipo < 0.5) mult *= 1 - _Pulso + _Pulso * sin(t * 2.2);
                else if (tipo > 2.5 && tipo < 3.5)
                {
                    // v local do feixe: atlas 0.5..1 em V
                    float v = saturate((i.uv.y - 0.5) * 2);
                    float faixa = frac(t * _VelocidadeFeixe + fase);
                    float onda = exp(-pow((v - faixa) * 7, 2));
                    mult *= 0.45 + 0.25 * sin(t * 1.7 + fase * 6.283) + onda * 0.9;
                }

                cor.a = saturate(cor.a * mult * _Opacidade);
                return cor;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
