using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#pragma warning disable 618 // FindObjectsByType com FindObjectsSortMode: mantido por compatibilidade com Unity 6.0/6.3 LTS

namespace AmbienteBB.VR
{
    // Textos de interface que não vêm do textos-cards.json (introdução, botões, dicas e card final).
    [System.Serializable]
    public class TextosInterface
    {
        [Header("Introdução")]
        public string etiquetaIntro = "Desafio BB Seguros";
        public string tituloIntro = "Encontre os riscos. Ative a proteção.";
        [TextArea(2, 4)] public string textoIntro = "Aponte o controle para cada imprevisto da casa e aperte o gatilho. Para andar, use o analógico ou aponte para um círculo no chão.";
        public string botaoIntro = "Começar";
        public string dicaIntro = "Aperte o gatilho para começar";

        [Header("Card do risco")]
        public string etiquetaRisco = "Risco identificado";
        public string botaoConsertar = "Consertar";
        public string botaoConsertando = "Consertando...";
        public string dicaSegurar = "Segure o gatilho para consertar";

        [Header("Card de proteção")]
        public string tituloProtegido = "Muito bem!";
        [TextArea(1, 3)] public string subtituloProtegido = "Não se preocupe, garantimos a proteção completa.";
        [Tooltip("{0} = nome do produto (ex.: Seguro Residencial).")]
        public string comoAjuda = "Como o {0} ajuda";
        public string nota = "*Consulte condições gerais";
        public string botaoContinuar = "Continuar";
        public string dicaContinuar = "Aperte o gatilho para continuar";

        [Header("Pet (só se 'petSomenteNoFinal' estiver ligado)")]
        public string etiquetaPet = "Surpresa";
        public string tituloPet = "Falta alguém importante";
        [TextArea(2, 4)] public string textoPet = "A casa está protegida. Mas tem alguém da família que também merece cuidado. Encontre o pet!";
        public string botaoPet = "Procurar";

        [Header("Contador e card final")]
        [Tooltip("{0} = encontrados, {1} = total.")]
        public string contador = "{0} de {1} encontrados";
        public string tituloFinal = "Você encontrou os riscos!";
        [TextArea(1, 3)] public string subtituloFinal = "A BB Seguros ajuda a proteger tudo o que importa.";
        public string passo1Final = "Pode retirar os óculos.";
        [TextArea(1, 3)] public string passo2Final = "No estande, aponte o celular para o QR Code e conheça as coberturas.";
        public string botaoFinal = "Jogar novamente";
        public string dicaFinal = "Aperte o gatilho para jogar novamente";
    }

    // Máquina de estados do desafio "Encontre os riscos. Ative a proteção."
    // Introdução -> Explorando (chip "N de 7") -> Mensagem (card do risco; segurar o gatilho em Consertar)
    // -> Feedback (card de proteção; gatilho para continuar) -> ... -> Concluído (card final; gatilho = jogar novamente).
    public class JogoRiscos : MonoBehaviour
    {
        public enum Fase { Introducao, Explorando, Mensagem, Feedback, Concluido }

        [Header("Referências")]
        public PainelVR painel;
        public RigVR rig;
        [Tooltip("Prefab de partículas tocado ao proteger um risco (PRF_VFX_Conserto_Azul).")]
        public GameObject vfxProtecao;
        public Material materialIndicador;
        public Color corIndicador = new Color(0.35f, 0.43f, 1f, 0.75f);
        public Color corIndicadorHover = new Color(0.99f, 0.99f, 0.19f, 1f);
        [Tooltip("Ícone do card de introdução (e do aviso do pet).")]
        public Sprite iconeIntro;

        [Header("Regras")]
        [Tooltip("Segundos segurando o gatilho para consertar (equivale ao 'arraste' da versão mobile).")]
        public float tempoSegurar = 1.0f;
        [Tooltip("Ligado: o cachorro só fica interativo depois de todos os outros riscos (elemento-surpresa). " +
                 "Desligado: ele pode ser encontrado a qualquer momento, como os demais.")]
        public bool petSomenteNoFinal = false;
        [Tooltip("Ligado: a cada partida sorteia 1 risco de cada categoria (vazamento de água, curto-circuito, incêndio, gás, " +
                 "janela/porta, assistência doméstica e pet). Os não sorteados aparecem consertados.")]
        public bool sortearPorCategoria = true;
        [Tooltip("Segundos no card final antes de o gatilho aceitar 'Jogar novamente' (evita reiniciar sem querer).")]
        public float esperaAntesDeReiniciar = 2f;
        [Tooltip("Segundos após a conclusão para voltar sozinho à introdução (0 = só pelo botão).")]
        public float tempoReinicioAutomatico = 60f;
        public bool mostrarCronometro = true;

        public TextosInterface textos = new TextosInterface();

        [Header("Áudio (opcional)")]
        public AudioSource fonteAudio;
        public AudioClip somInicio;
        public AudioClip somIdentificar;
        public AudioClip somProteger;
        public AudioClip somPet;
        public AudioClip somConclusao;

        public Fase FaseAtual { get; private set; }
        public int Total { get; private set; }
        public int Protegidos { get; private set; }

        readonly List<PontoDeRisco> candidatos = new List<PontoDeRisco>();
        readonly List<PontoDeRisco> riscos = new List<PontoDeRisco>();
        readonly List<ApontadorVR> apontadores = new List<ApontadorVR>();
        readonly List<PontoDeTeleporte> teleportes = new List<PontoDeTeleporte>();
        PontoDeRisco pet;
        PontoDeRisco riscoAtual;
        float segurando;
        bool aguardandoSoltar;
        bool petLiberado;
        bool petAnunciado;
        float tempoFase;
        float tempoInicio;
        Vector3 posInicialRig;
        Quaternion rotInicialRig;
        InputAction acaoReiniciar;
        float segurandoReinicio;

        void Awake()
        {
            // Operador: segurar o botão Menu do controle esquerdo (ou a tecla R) por 1,5 s reinicia a qualquer momento.
            acaoReiniciar = new InputAction("Reiniciar", InputActionType.Button, "<XRController>{LeftHand}/menu");
            acaoReiniciar.AddBinding("<Keyboard>/r");
        }

        void OnEnable() => acaoReiniciar.Enable();
        void OnDisable() => acaoReiniciar.Disable();
        void OnDestroy() => acaoReiniciar.Dispose();

        void Start()
        {
            foreach (var r in FindObjectsByType<PontoDeRisco>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (r.dados == null)
                {
                    Debug.LogWarning("[JogoRiscos] " + r.name + " não tem DadosRisco; ignorado.", r);
                    continue;
                }
                if (!r.dados.ativoNoDesafio)
                {
                    r.Proteger(); // fora do desafio: fica consertado na casa
                    continue;
                }
                candidatos.Add(r);
            }
            candidatos.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            apontadores.AddRange(FindObjectsByType<ApontadorVR>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            teleportes.AddRange(FindObjectsByType<PontoDeTeleporte>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            foreach (var r in candidatos)
            {
                r.Reiniciar();
                var ind = IndicadorRisco.Criar(r, materialIndicador);
                ind.corPendente = corIndicador;
                ind.corHover = corIndicadorHover;
            }
            if (rig != null)
            {
                posInicialRig = rig.transform.position;
                rotInicialRig = rig.transform.rotation;
            }
            if (candidatos.Count == 0) Debug.LogWarning("[JogoRiscos] Nenhum PontoDeRisco ativo no desafio. Rode 'BB Seguros > 2. Configurar cena VR'.");
            IrParaIntroducao();
        }

        // Escolhe os riscos desta partida: um por categoria (sorteado entre os candidatos da categoria).
        // Os que não entram ficam com a cenografia consertada.
        void Sortear()
        {
            riscos.Clear();
            var grupos = new Dictionary<string, List<PontoDeRisco>>();
            var ordem = new List<string>();
            foreach (var r in candidatos)
            {
                var cat = r.dados.categoria;
                string chave = (!sortearPorCategoria || cat == CategoriaRisco.Nenhuma) ? "obj:" + r.name : cat.ToString();
                if (!grupos.TryGetValue(chave, out var lista))
                {
                    lista = new List<PontoDeRisco>();
                    grupos[chave] = lista;
                    ordem.Add(chave);
                }
                lista.Add(r);
            }
            var nomes = new List<string>();
            foreach (var chave in ordem)
            {
                var lista = grupos[chave];
                var escolhido = lista[Random.Range(0, lista.Count)];
                foreach (var r in lista)
                {
                    if (r == escolhido) r.Reiniciar();
                    else r.Proteger();
                }
                riscos.Add(escolhido);
                if (lista.Count > 1) nomes.Add(escolhido.name);
            }
            pet = riscos.Find(r => r.EPet);
            Total = riscos.Count;
            if (nomes.Count > 0) Debug.Log("[JogoRiscos] Sorteados nesta partida: " + string.Join(", ", nomes));
        }

        void IrParaIntroducao()
        {
            FaseAtual = Fase.Introducao;
            tempoFase = 0f;
            Protegidos = 0;
            segurando = 0f;
            riscoAtual = null;
            petLiberado = false;
            petAnunciado = false;
            aguardandoSoltar = true;
            Sortear();
            if (pet != null && !petSomenteNoFinal) pet.DefinirEstado(EstadoRisco.Pendente);
            DefinirTeleportes(false);
            if (painel != null)
            {
                painel.DefinirProgresso("");
                painel.MostrarRisco(iconeIntro, textos.etiquetaIntro, textos.tituloIntro, textos.textoIntro, textos.botaoIntro, textos.dicaIntro);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            tempoFase += dt;

            if (acaoReiniciar.IsPressed())
            {
                segurandoReinicio += dt;
                if (segurandoReinicio > 1.5f)
                {
                    segurandoReinicio = -999f;
                    Reiniciar();
                    return;
                }
            }
            else segurandoReinicio = 0f;

            bool algumPressionado = false;
            bool algumApertou = false;
            ApontadorVR mirandoRisco = null;
            ApontadorVR mirandoTele = null;
            foreach (var a in apontadores)
            {
                if (a == null || !a.isActiveAndEnabled || !a.Ativo) continue;
                algumPressionado |= a.GatilhoPressionado;
                algumApertou |= a.GatilhoApertadoNesteQuadro;
                if (a.RiscoSobMira != null) mirandoRisco = a;
                if (a.TeleporteSobMira != null) mirandoTele = a;
            }
            if (aguardandoSoltar)
            {
                if (!algumPressionado) aguardandoSoltar = false;
                algumApertou = false;
                algumPressionado = false;
            }

            switch (FaseAtual)
            {
                case Fase.Introducao:
                    if (algumApertou) Iniciar();
                    break;

                case Fase.Explorando:
                    if (algumApertou)
                    {
                        if (mirandoRisco != null) Identificar(mirandoRisco.RiscoSobMira, mirandoRisco);
                        else if (mirandoTele != null && rig != null)
                        {
                            rig.Teleportar(mirandoTele.TeleporteSobMira.Destino);
                            mirandoTele.Vibrar(0.2f, 0.05f);
                        }
                    }
                    break;

                case Fase.Mensagem:
                    if (algumPressionado) segurando += dt;
                    else segurando = Mathf.Max(0f, segurando - dt * 2f);
                    if (painel != null)
                    {
                        painel.DefinirPreenchimento(segurando / tempoSegurar);
                        painel.DefinirTextoBotao(segurando > 0.05f ? textos.botaoConsertando : textos.botaoConsertar);
                    }
                    if (segurando >= tempoSegurar) Proteger();
                    break;

                case Fase.Feedback:
                    // O meio segundo evita pular o card com o mesmo aperto que consertou.
                    if (algumApertou && tempoFase > 0.5f)
                    {
                        if (petLiberado && !petAnunciado) AnunciarPet();
                        else VoltarAExplorar();
                    }
                    break;

                case Fase.Concluido:
                    if (algumApertou && tempoFase >= esperaAntesDeReiniciar) Reiniciar();
                    else if (tempoReinicioAutomatico > 0f && tempoFase >= tempoReinicioAutomatico) Reiniciar();
                    break;
            }
        }

        void Iniciar()
        {
            FaseAtual = Fase.Explorando;
            tempoFase = 0f;
            tempoInicio = Time.time;
            DefinirTeleportes(true);
            AtualizarProgresso();
            if (painel != null) painel.Compactar();
            Tocar(somInicio, null);
        }

        void Identificar(PontoDeRisco risco, ApontadorVR apontador)
        {
            riscoAtual = risco;
            FaseAtual = Fase.Mensagem;
            tempoFase = 0f;
            segurando = 0f;
            aguardandoSoltar = true;
            risco.Hover(false);

            var d = risco.dados;
            if (painel != null)
                painel.MostrarRisco(d.icone, textos.etiquetaRisco, d.titulo, d.textoRisco, textos.botaoConsertar, textos.dicaSegurar);
            if (apontador != null) apontador.Vibrar(0.5f, 0.1f);
            Tocar(somIdentificar, null);
        }

        void Proteger()
        {
            var r = riscoAtual;
            r.Proteger();
            Protegidos++;
            Vector3 pos = r.PosicaoIndicador;
            if (vfxProtecao != null)
            {
                var vfx = Instantiate(vfxProtecao, pos, Quaternion.identity);
                Destroy(vfx, 6f);
            }
            foreach (var a in apontadores) if (a != null && a.isActiveAndEnabled) a.Vibrar(0.7f, 0.15f);
            Tocar(somProteger, pos);
            AtualizarProgresso();

            FaseAtual = Fase.Feedback;
            tempoFase = 0f;
            aguardandoSoltar = true;
            if (painel != null)
            {
                var d = r.dados;
                List<AjudaSeguro> ajudas = d.ajudas;
                if (ajudas == null || ajudas.Count == 0)
                    ajudas = new List<AjudaSeguro> { new AjudaSeguro { cobertura = d.coberturas, texto = d.textoSeguro } };
                painel.MostrarProtegido(textos.tituloProtegido, textos.subtituloProtegido,
                    string.Format(textos.comoAjuda, d.NomeProduto), ajudas, textos.nota, textos.botaoContinuar, textos.dicaContinuar);
            }

            int semPet = pet != null ? Total - 1 : Total;
            if (pet != null && petSomenteNoFinal && !petLiberado && Protegidos >= semPet && pet.Estado != EstadoRisco.Protegido)
            {
                petLiberado = true;
                pet.DefinirEstado(EstadoRisco.Pendente);
            }
        }

        // Só com 'petSomenteNoFinal': aviso de que falta o pet, também fechado com o gatilho.
        void AnunciarPet()
        {
            petAnunciado = true;
            tempoFase = 0f;
            aguardandoSoltar = true;
            if (painel != null)
                painel.MostrarRisco(iconeIntro, textos.etiquetaPet, textos.tituloPet, textos.textoPet, textos.botaoPet, textos.dicaContinuar);
            Tocar(somPet, pet != null ? pet.PosicaoIndicador : (Vector3?)null);
        }

        void VoltarAExplorar()
        {
            if (Protegidos >= Total)
            {
                Concluir();
                return;
            }
            FaseAtual = Fase.Explorando;
            tempoFase = 0f;
            aguardandoSoltar = true;
            if (painel != null) painel.Compactar();
        }

        void Concluir()
        {
            FaseAtual = Fase.Concluido;
            tempoFase = 0f;
            aguardandoSoltar = true;
            DefinirTeleportes(false);
            string resumo = string.Format(textos.contador, Protegidos, Total);
            if (mostrarCronometro) resumo += "  •  Tempo: " + FormatarTempo(Time.time - tempoInicio);
            if (painel != null)
                painel.MostrarFinal(textos.tituloFinal, textos.subtituloFinal, textos.passo1Final, textos.passo2Final,
                    resumo, textos.botaoFinal, textos.dicaFinal);
            Tocar(somConclusao, null);
        }

        // Volta ao início para o próximo participante: riscos, posição do jogador e contagem.
        public void Reiniciar()
        {
            if (rig != null) rig.ReposicionarNoInicio(posInicialRig, rotInicialRig);
            IrParaIntroducao();
        }

        void AtualizarProgresso()
        {
            if (painel != null) painel.DefinirProgresso(string.Format(textos.contador, Protegidos, Total));
        }

        void DefinirTeleportes(bool visiveis)
        {
            foreach (var t in teleportes) if (t != null) t.DefinirVisivel(visiveis);
        }

        void Tocar(AudioClip clip, Vector3? posicao)
        {
            if (clip == null) return;
            if (fonteAudio != null) fonteAudio.PlayOneShot(clip);
            else
            {
                Vector3 p = posicao ?? (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
                AudioSource.PlayClipAtPoint(clip, p);
            }
        }

        static string FormatarTempo(float segundos)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(segundos));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }
}
