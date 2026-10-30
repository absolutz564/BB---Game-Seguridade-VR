using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AmbienteBB.VR
{
    // Painel de mensagens em espaço de mundo, montado com os cards do Danilo (Assets/Sprites/cards):
    // card de risco ("Risco identificado" + Consertar), card de proteção ("Muito bem!" + coberturas + Continuar)
    // e card final ("Você encontrou os riscos!" + Jogar novamente). Durante a exploração vira um chip com o contador.
    //
    // O canvas é desenhado em pixels do layout (1280 px de largura) e escalado para ocupar "larguraGraus" do
    // campo de visão a "distancia" metros, como pede a especificação (~25° a ~1,5 m).
    public class PainelVR : MonoBehaviour
    {
        public const float LarguraDesign = 1280f;

        [Header("Card do risco")]
        public GameObject cartaoRisco;
        public Image riscoIcone;
        public TMP_Text riscoEtiqueta;
        public TMP_Text riscoTitulo;
        public TMP_Text riscoDescricao;
        public TMP_Text riscoBotao;
        public RectTransform riscoPreenchimento;
        public TMP_Text riscoDica;

        [Header("Card de proteção")]
        public GameObject cartaoProtegido;
        public TMP_Text protTitulo;
        public TMP_Text protSubtitulo;
        public TMP_Text protComoAjuda;
        public GameObject[] protItens = new GameObject[2];
        public TMP_Text[] protCoberturas = new TMP_Text[2];
        public TMP_Text[] protTextos = new TMP_Text[2];
        public TMP_Text protNota;
        public TMP_Text protBotao;
        public TMP_Text protDica;

        [Header("Card final")]
        public GameObject cartaoFinal;
        public TMP_Text finalTitulo;
        public TMP_Text finalSubtitulo;
        public TMP_Text finalPasso1;
        public TMP_Text finalPasso2;
        public TMP_Text finalResumo;
        public TMP_Text finalBotao;
        public TMP_Text finalDica;

        [Header("Chip (modo compacto)")]
        public GameObject chip;
        public TMP_Text chipTexto;

        public CanvasGroup grupo;

        [Header("Tamanho e posição (especificação: ~25° de largura a ~1,5 m)")]
        public bool seguirCamera = true;
        [Tooltip("Distância do painel aos olhos, em metros.")]
        public float distancia = 1.5f;
        [Tooltip("Se houver parede ou móvel no caminho, o painel chega mais perto até este limite (mantendo o mesmo tamanho aparente).")]
        public float distanciaMinima = 0.6f;
        [Tooltip("Largura aparente do card, em graus do campo de visão.")]
        public float larguraGraus = 25f;
        [Tooltip("Quantos graus o centro do card fica abaixo do centro do olhar.")]
        public float anguloCartao = 4f;
        [Tooltip("Quantos graus o chip fica abaixo do centro do olhar.")]
        public float anguloChip = 24f;
        [Tooltip("Quanto o painel acompanha o olhar para baixo, em graus.")]
        public float inclinacaoMaximaBaixo = 60f;
        [Tooltip("Quanto o painel acompanha o olhar para cima, em graus.")]
        public float inclinacaoMaximaCima = 40f;
        public float suavizacaoPosicao = 6f;
        public float suavizacaoRotacao = 7f;
        public LayerMask camadasObstaculo = ~0;

        public bool Compacto { get; private set; }

        RectTransform preenchimentoAtivo;
        TMP_Text botaoAtivo;
        TMP_Text dicaAtiva;
        bool posicionado;
        float distanciaAtual;

        void Awake()
        {
            // Painéis montados antes deste ajuste: centraliza o texto das etiquetas (pílulas) sem precisar recriar.
            CentralizarEtiqueta(riscoEtiqueta);
            if (protCoberturas != null) foreach (var t in protCoberturas) CentralizarEtiqueta(t);
        }

        static void CentralizarEtiqueta(TMP_Text t)
        {
            if (t == null) return;
            t.alignment = TextAlignmentOptions.Center;
            var h = t.transform.parent != null ? t.transform.parent.GetComponent<HorizontalLayoutGroup>() : null;
            if (h == null) return;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
        }

        // ------------------------------------------------------------------ cards

        public void MostrarRisco(Sprite icone, string etiqueta, string titulo, string descricao, string botao, string dica)
        {
            if (riscoIcone != null)
            {
                riscoIcone.sprite = icone;
                riscoIcone.enabled = icone != null;
            }
            Definir(riscoEtiqueta, etiqueta);
            Definir(riscoTitulo, titulo);
            Definir(riscoDescricao, descricao);
            Definir(riscoBotao, botao);
            Definir(riscoDica, dica);
            Ativar(cartaoRisco, riscoPreenchimento, riscoBotao, riscoDica);
        }

        public void MostrarProtegido(string titulo, string subtitulo, string comoAjuda, IList<AjudaSeguro> ajudas, string nota, string botao, string dica)
        {
            Definir(protTitulo, titulo);
            Definir(protSubtitulo, subtitulo);
            Definir(protComoAjuda, comoAjuda);
            for (int i = 0; i < protItens.Length; i++)
            {
                bool tem = ajudas != null && i < ajudas.Count && ajudas[i] != null && !string.IsNullOrEmpty(ajudas[i].cobertura);
                if (protItens[i] != null) protItens[i].SetActive(tem);
                if (!tem) continue;
                if (i < protCoberturas.Length) Definir(protCoberturas[i], ajudas[i].cobertura);
                if (i < protTextos.Length) Definir(protTextos[i], ajudas[i].texto);
            }
            Definir(protNota, nota);
            if (protNota != null) protNota.gameObject.SetActive(!string.IsNullOrEmpty(nota));
            Definir(protBotao, botao);
            Definir(protDica, dica);
            Ativar(cartaoProtegido, null, protBotao, protDica);
        }

        public void MostrarFinal(string titulo, string subtitulo, string passo1, string passo2, string resumo, string botao, string dica)
        {
            Definir(finalTitulo, titulo);
            Definir(finalSubtitulo, subtitulo);
            Definir(finalPasso1, passo1);
            Definir(finalPasso2, passo2);
            Definir(finalResumo, resumo);
            if (finalResumo != null) finalResumo.gameObject.SetActive(!string.IsNullOrEmpty(resumo));
            Definir(finalBotao, botao);
            Definir(finalDica, dica);
            Ativar(cartaoFinal, null, finalBotao, finalDica);
        }

        void Ativar(GameObject cartao, RectTransform preenchimento, TMP_Text botao, TMP_Text dica)
        {
            if (grupo != null) grupo.alpha = 1f;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            // Ao sair do chip, o card aparece na hora onde a pessoa está olhando (sem deslizar).
            if (Compacto) posicionado = false;
            Compacto = false;
            foreach (var c in new[] { cartaoRisco, cartaoProtegido, cartaoFinal })
                if (c != null) c.SetActive(c == cartao);
            if (chip != null) chip.SetActive(false);
            preenchimentoAtivo = preenchimento;
            botaoAtivo = botao;
            dicaAtiva = dica;
            DefinirPreenchimento(0f);
            if (cartao != null) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)cartao.transform);
        }

        static void Definir(TMP_Text t, string texto)
        {
            if (t != null) t.text = texto ?? "";
        }

        // Só o chip com o contador; libera a visão da cena.
        public void Compactar()
        {
            Compacto = true;
            foreach (var c in new[] { cartaoRisco, cartaoProtegido, cartaoFinal })
                if (c != null) c.SetActive(false);
            if (chip != null) chip.SetActive(chipTexto != null && !string.IsNullOrEmpty(chipTexto.text));
        }

        public void DefinirDica(string texto) => Definir(dicaAtiva, texto);

        public void DefinirTextoBotao(string texto) => Definir(botaoAtivo, texto);

        public void DefinirProgresso(string texto)
        {
            if (chipTexto != null) chipTexto.text = texto ?? "";
            if (Compacto && chip != null) chip.SetActive(!string.IsNullOrEmpty(texto));
        }

        // Preenche o botão "Consertar" da esquerda para a direita enquanto o gatilho está segurado.
        public void DefinirPreenchimento(float valor)
        {
            if (preenchimentoAtivo == null) return;
            var pai = preenchimentoAtivo.parent as RectTransform;
            float largura = pai != null ? pai.rect.width : 0f;
            preenchimentoAtivo.sizeDelta = new Vector2(Mathf.Clamp01(valor) * largura, preenchimentoAtivo.sizeDelta.y);
        }

        public void Esconder()
        {
            if (grupo != null) grupo.alpha = 0f;
        }

        public void PosicionarImediato()
        {
            posicionado = false;
            LateUpdate();
        }

        // ------------------------------------------------------------------ acompanhamento do olhar

        void LateUpdate()
        {
            if (!seguirCamera) return;
            var cam = Camera.main;
            if (cam == null) return;
            Transform c = cam.transform;

            Vector3 frente = c.forward;
            Vector3 plano = Vector3.ProjectOnPlane(frente, Vector3.up);
            if (plano.sqrMagnitude < 0.0001f) plano = Vector3.ProjectOnPlane(transform.position - c.position, Vector3.up);
            if (plano.sqrMagnitude < 0.0001f) plano = Vector3.forward;
            plano.Normalize();
            Vector3 direita = Vector3.Cross(Vector3.up, plano).normalized;

            // Inclinação do olhar limitada + deslocamento do card/chip para baixo, tudo em graus.
            float inclinacao = Mathf.Asin(Mathf.Clamp(frente.y, -1f, 1f)) * Mathf.Rad2Deg;
            inclinacao = Mathf.Clamp(inclinacao, -inclinacaoMaximaBaixo, inclinacaoMaximaCima);
            inclinacao -= Compacto ? anguloChip : anguloCartao;
            inclinacao = Mathf.Clamp(inclinacao, -80f, 80f);
            Vector3 dir = Quaternion.AngleAxis(-inclinacao, direita) * plano;

            float d = distancia;
            if (Physics.Raycast(c.position, dir, out RaycastHit hit, distancia + 0.2f, camadasObstaculo, QueryTriggerInteraction.Ignore))
                d = Mathf.Max(distanciaMinima, hit.distance - 0.2f);

            Vector3 alvo = c.position + dir * d;
            Vector3 cima = Vector3.Cross(dir, direita);
            Quaternion rot = Quaternion.LookRotation(dir, cima);

            if (!posicionado)
            {
                distanciaAtual = d;
                transform.SetPositionAndRotation(alvo, rot);
                posicionado = true;
            }
            else
            {
                float kp = 1f - Mathf.Exp(-suavizacaoPosicao * Time.deltaTime);
                float kr = 1f - Mathf.Exp(-suavizacaoRotacao * Time.deltaTime);
                distanciaAtual = Mathf.Lerp(distanciaAtual, d, kp);
                transform.position = Vector3.Lerp(transform.position, alvo, kp);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, kr);
            }

            // Mantém a mesma largura aparente mesmo quando o painel chega mais perto por causa de uma parede.
            float dist = Mathf.Max(0.1f, Vector3.Distance(transform.position, c.position));
            float larguraMetros = 2f * dist * Mathf.Tan(larguraGraus * 0.5f * Mathf.Deg2Rad);
            transform.localScale = Vector3.one * (larguraMetros / LarguraDesign);
        }
    }
}
