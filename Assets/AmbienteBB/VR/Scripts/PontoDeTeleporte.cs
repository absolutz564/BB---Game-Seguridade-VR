using UnityEngine;

namespace AmbienteBB.VR
{
    // Disco no chão: apontar + gatilho leva o participante até aqui (locomoção por teleporte, sem enjoo).
    public class PontoDeTeleporte : MonoBehaviour
    {
        public string nomeAmbiente = "Sala";
        public Color corNormal = new Color(0.25f, 0.6f, 1f, 0.55f);
        public Color corHover = new Color(1f, 0.85f, 0.2f, 0.95f);

        Renderer rend;
        Collider colisor;
        MaterialPropertyBlock bloco;
        bool visivel = true;
        static readonly int IdCor = Shader.PropertyToID("_BaseColor");
        static readonly int IdCorLegado = Shader.PropertyToID("_Color");

        public Vector3 Destino => transform.position;

        void Awake()
        {
            rend = GetComponentInChildren<Renderer>();
            colisor = GetComponentInChildren<Collider>();
            Hover(false);
        }

        public void Hover(bool ativo)
        {
            if (rend == null) return;
            if (bloco == null) bloco = new MaterialPropertyBlock();
            Color cor = ativo ? corHover : corNormal;
            bloco.SetColor(IdCor, cor);
            bloco.SetColor(IdCorLegado, cor);
            rend.SetPropertyBlock(bloco);
            float s = ativo ? 1.15f : 1f;
            rend.transform.localScale = new Vector3(s, s, 1f);
        }

        public void DefinirVisivel(bool valor)
        {
            visivel = valor;
            if (rend != null) rend.enabled = valor;
            if (colisor != null) colisor.enabled = valor;
        }

        public bool Visivel => visivel;
    }
}
