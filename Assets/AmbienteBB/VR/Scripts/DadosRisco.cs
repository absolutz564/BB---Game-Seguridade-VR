using System.Collections.Generic;
using UnityEngine;

namespace AmbienteBB.VR
{
    public enum ProdutoSeguro { SeguroResidencial, SeguroVida }

    // Os 7 pontos de descoberta do escopo. A cada partida o jogo sorteia um risco de cada categoria.
    // "Nenhuma" = o risco entra sempre (fora do sorteio).
    public enum CategoriaRisco { Nenhuma, VazamentoAgua, CurtoCircuito, PrincipioIncendio, VazamentoGas, JanelaOuPorta, AssistenciaDomestica, Pet }

    [System.Serializable]
    public class AjudaSeguro
    {
        [Tooltip("Nome da cobertura ou assistência (vai na etiqueta amarela).")]
        public string cobertura;
        [TextArea(1, 3)] public string texto;
    }

    // Conteúdo de um ponto de descoberta: o que o participante lê ao identificar e ao consertar o risco.
    // Os textos vêm de Assets/Sprites/cards/textos-cards.json (menu BB Seguros > 1) e ficam em assets,
    // um por risco, para homologação sem mexer na cena.
    [CreateAssetMenu(menuName = "BB Seguros/Dados do Risco", fileName = "RISCO_Novo")]
    public class DadosRisco : ScriptableObject
    {
        [Tooltip("Nome exato do objeto na cena (ex.: risco_VazamentoAgua_01). O configurador usa isso para ligar o asset ao objeto.")]
        public string chaveObjeto;
        [Tooltip("Número do card no textos-cards.json (01 a 10). Vazio = asset sem card do Danilo.")]
        public string numeroCard;

        [Header("Card do risco")]
        public Sprite icone;
        public string titulo;
        [Tooltip("Descrição curta do que está fora do lugar.")]
        [TextArea(2, 4)] public string textoRisco;

        [Header("Card de proteção")]
        [Tooltip("Nome do produto como aparece em \"Como o ... ajuda\" (ex.: Seguro Residencial).")]
        public string nomeProduto;
        [Tooltip("No máximo duas coberturas por card.")]
        public List<AjudaSeguro> ajudas = new List<AjudaSeguro>();
        [Tooltip("Marcado no JSON como pendente de homologação.")]
        public bool aHomologar;

        [Header("Textos antigos (antes dos cards do Danilo; não são mais exibidos)")]
        [TextArea(2, 5)] public string textoSeguro;
        public string coberturas;
        public ProdutoSeguro produto = ProdutoSeguro.SeguroResidencial;

        [Header("Regras do desafio")]
        [Tooltip("Pode ser sorteado no desafio. Desligado = fica sempre consertado (só cenografia).")]
        public bool ativoNoDesafio = true;
        [Tooltip("Ponto de descoberta a que este risco pertence. O jogo sorteia um risco por categoria a cada partida.")]
        public CategoriaRisco categoria = CategoriaRisco.Nenhuma;
        [Tooltip("É o pet (cachorro).")]
        public bool ePet;

        public string NomeProduto
        {
            get
            {
                if (!string.IsNullOrEmpty(nomeProduto)) return nomeProduto;
                return produto == ProdutoSeguro.SeguroVida ? "Seguro Vida" : "Seguro Residencial";
            }
        }
    }
}
