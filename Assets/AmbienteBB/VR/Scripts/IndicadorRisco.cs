using UnityEngine;

namespace AmbienteBB.VR
{
    // Halo pulsante que flutua sobre o risco e sempre olha para o participante.
    // Criado em tempo de execução pelo JogoRiscos (um por PontoDeRisco).
    public class IndicadorRisco : MonoBehaviour
    {
        public Color corPendente = new Color(0.25f, 0.6f, 1f, 0.75f);
        public Color corHover = new Color(1f, 0.85f, 0.2f, 1f);
        public float tamanho = 0.28f;
        public float amplitudePulso = 0.12f;
        public float velocidadePulso = 2.2f;

        Renderer rend;
        MaterialPropertyBlock bloco;
        bool hover;
        bool visivel;
        float escalaAtual;
        static readonly int IdCor = Shader.PropertyToID("_BaseColor");
        static readonly int IdCorLegado = Shader.PropertyToID("_Color");

        public static IndicadorRisco Criar(PontoDeRisco ponto, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Indicador_" + ponto.name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.position = ponto.PosicaoIndicador;
            var ind = go.AddComponent<IndicadorRisco>();
            ind.rend = go.GetComponent<Renderer>();
            if (material != null) ind.rend.sharedMaterial = material;
            ind.rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ind.rend.receiveShadows = false;
            ind.rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            ind.rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            ponto.Indicador = ind;
            ind.AtualizarEstado(ponto.Estado);
            return ind;
        }

        public void AtualizarEstado(EstadoRisco estado)
        {
            visivel = estado == EstadoRisco.Pendente;
            if (!visivel) hover = false;
            if (rend != null) rend.enabled = visivel;
        }

        public void Hover(bool ativo) => hover = ativo;

        public void Reposicionar(Vector3 pos) => transform.position = pos;

        void LateUpdate()
        {
            if (!visivel || rend == null) return;
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 dir = transform.position - cam.transform.position;
                if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
            }
            float pulso = 1f + Mathf.Sin(Time.time * velocidadePulso * Mathf.PI) * amplitudePulso;
            float alvo = tamanho * pulso * (hover ? 1.35f : 1f);
            if (escalaAtual <= 0f) escalaAtual = alvo;
            escalaAtual = Mathf.Lerp(escalaAtual, alvo, Time.deltaTime * 10f);
            transform.localScale = Vector3.one * escalaAtual;

            if (bloco == null) bloco = new MaterialPropertyBlock();
            Color cor = hover ? corHover : corPendente;
            bloco.SetColor(IdCor, cor);
            bloco.SetColor(IdCorLegado, cor);
            rend.SetPropertyBlock(bloco);
        }
    }
}
