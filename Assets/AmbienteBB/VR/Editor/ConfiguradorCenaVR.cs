using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if BB_XR_MANAGEMENT
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;
#endif
#if BB_OPENXR
using UnityEditor.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR;
#endif

#pragma warning disable 618 // FindObjectsByType com FindObjectsSortMode: mantido por compatibilidade com Unity 6.0/6.3 LTS

namespace AmbienteBB.VR.Editor
{
    // Menu "BB Seguros": monta a experiência VR na cena AmbienteBB sem trabalho manual no editor.
    // Pode ser executado várias vezes; só cria o que ainda não existe.
    public static class ConfiguradorCenaVR
    {
        const string PastaVR = "Assets/AmbienteBB/VR";
        const string PastaDados = PastaVR + "/Dados";
        const string PastaMateriais = PastaVR + "/Materiais";
        const string PastaTexturas = PastaVR + "/Texturas";
        const string CaminhoCena = "Assets/AmbienteBB/Cenas/AmbienteBB.unity";
        const string CaminhoShader = PastaVR + "/Shaders/SH_BB_Indicador.shader";
        const string CaminhoVfxConserto = "Assets/AmbienteBB/VFX/PRF_VFX_Conserto_Azul.prefab";
        const string CaminhoPrefabCachorro = "Assets/AmbienteBB/Prefabs/PRF_Cachorro.prefab";
        const string ChaveCachorro = "Cachorro";

        // Paleta da referência "BB Play na proteção": azul #3142B3, azul claro #596EFF, lilás #C8CFFF, amarelo #FCFC30.
        static readonly Color AzulBB = new Color32(0x31, 0x42, 0xB3, 0xFF);
        static readonly Color AzulClaroBB = new Color32(0x59, 0x6E, 0xFF, 0xFF);
        static readonly Color LilasBB = new Color32(0xC8, 0xCF, 0xFF, 0xFF);
        static readonly Color AmareloBB = new Color32(0xFC, 0xFC, 0x30, 0xFF);
        static readonly Color CinzaTexto = new Color(0.16f, 0.18f, 0.24f);

        struct DefRisco
        {
            public string chave, titulo, risco, seguro, coberturas;
            public ProdutoSeguro produto;
            public bool ativo, pet;
        }

        // Textos de partida (baseados no "Material Convenção BB Seguros"). Sujeitos à homologação de
        // Produto, Marketing e Jurídico/Compliance: edite nos assets em Assets/AmbienteBB/VR/Dados.
        static readonly DefRisco[] Definicoes =
        {
            new DefRisco { chave = "risco_VazamentoAgua_01", titulo = "Vazamento de água", ativo = true,
                risco = "A porca do sifão está solta: pinga água e já formou uma poça dentro do gabinete da pia.",
                seguro = "Com o Seguro Residencial, a cobertura Danos por Água protege o imóvel e os bens contra rompimento de tubulações. E a assistência Casa em Apuros envia um encanador.",
                coberturas = "Danos por água • Casa em apuros (encanador)" },
            new DefRisco { chave = "risco_CurtoCircuito_02", titulo = "Curto-circuito", ativo = true,
                risco = "Um benjamim sobrecarregado está soltando faíscas e já deixou marca de queimado na parede.",
                seguro = "A cobertura Danos Elétricos do Seguro Residencial protege contra curto-circuito e variações de tensão, incluindo fiação e instalações. E a Casa em Apuros envia um eletricista.",
                coberturas = "Danos elétricos • Casa em apuros (eletricista)" },
            new DefRisco { chave = "risco_PrincipioIncendio_03", titulo = "Princípio de incêndio", ativo = true,
                risco = "A frigideira esquentou demais e começou a pegar fogo no fogão.",
                seguro = "A cobertura Incêndio do Seguro Residencial garante os danos materiais ao imóvel causados por incêndio, queda de raio ou fumaça.",
                coberturas = "Incêndio" },
            new DefRisco { chave = "risco_VazamentoGas_04", titulo = "Vazamento de gás", ativo = true,
                risco = "O aquecedor a gás está vazando. Sem manutenção, o risco só aumenta.",
                seguro = "A assistência Casa Segura do Seguro Residencial inclui manutenção de aquecedor a gás. E a cobertura Incêndio protege o imóvel se o pior acontecer.",
                coberturas = "Casa segura (manutenção de aquecedor a gás) • Incêndio" },
            new DefRisco { chave = "risco_JanelaVulneravel_05", titulo = "Janela vulnerável", ativo = true,
                risco = "Vidro trincado e trinco quebrado: a janela não fecha e deixa a casa exposta.",
                seguro = "Com a assistência Casa Segura, o Seguro Residencial instala tela de proteção e película. E a Casa em Apuros envia um chaveiro quando você precisar.",
                coberturas = "Casa segura (tela de proteção, película) • Casa em apuros (chaveiro)" },
            new DefRisco { chave = "risco_AssistenciaDomestica_06", titulo = "Assistência doméstica", ativo = true,
                risco = "Insetos na cozinha e manutenção acumulada: a casa está pedindo ajuda.",
                seguro = "O Seguro Residencial tem assistências para o dia a dia: Casa Sem Insetos (desinsetização), Casa Limpa e Casa Funcionando (conserto de eletrodomésticos).",
                coberturas = "Casa sem insetos • Casa limpa • Casa funcionando" },
            new DefRisco { chave = ChaveCachorro, titulo = "Alguém da família", ativo = true, pet = true, produto = ProdutoSeguro.SeguroVida,
                risco = "Esse aqui não é um risco: é parte da família. E também merece cuidado.",
                seguro = "Com o Seguro de Vida da BB Seguros você conta com a Assistência Pet: consulta veterinária e hospedagem do pet quando precisar.",
                coberturas = "Consulta veterinária • Hospedagem pet" },

            // Riscos extras já modelados na cena. Desligados no desafio (ativoNoDesafio = false) para manter os 7 pontos do escopo.
            new DefRisco { chave = "risco_Goteira_07", titulo = "Goteira no teto", ativo = false,
                risco = "Mancha no teto e poça no chão: a água está entrando pela laje.",
                seguro = "A cobertura Danos por Água do Seguro Residencial protege o imóvel contra água de chuva e temporal. E a Casa em Apuros faz cobertura provisória de telhados.",
                coberturas = "Danos por água • Casa em apuros (cobertura provisória de telhados)" },
            new DefRisco { chave = "risco_PortaArrombada_08", titulo = "Porta arrombada", ativo = false,
                risco = "Batente forçado e chave presa na fechadura: a porta não tranca mais.",
                seguro = "A assistência Casa Segura do Seguro Residencial instala fechadura eletrônica e faz instalações em portas. E a Casa em Apuros envia um chaveiro.",
                coberturas = "Casa segura (fechadura eletrônica) • Casa em apuros (chaveiro)" },
            new DefRisco { chave = "risco_LampadaPiscando_09", titulo = "Lâmpada piscando", ativo = false,
                risco = "A luminária pisca sem parar: sinal de problema na instalação elétrica.",
                seguro = "A cobertura Danos Elétricos do Seguro Residencial protege fiação e instalações. E a Casa em Apuros envia um eletricista.",
                coberturas = "Danos elétricos • Casa em apuros (eletricista)" },
            new DefRisco { chave = "risco_VazamentoChuveiro_10", titulo = "Vazamento no chuveiro", ativo = false,
                risco = "O box está pingando e o piso do banheiro vive molhado.",
                seguro = "A cobertura Danos por Água do Seguro Residencial protege contra rompimento de tubulações. E a Casa em Apuros envia um encanador.",
                coberturas = "Danos por água • Casa em apuros (encanador)" },
            new DefRisco { chave = "risco_ArCondicionado_11", titulo = "Ar-condicionado vazando", ativo = false,
                risco = "A aleta quebrou e o dreno está pingando na parede, formando mancha e poça.",
                seguro = "A assistência Casa Funcionando do Seguro Residencial conserta o ar-condicionado. E a Casa Limpa faz a limpeza do aparelho.",
                coberturas = "Casa funcionando (conserto de ar-condicionado) • Casa limpa (limpeza de ar-condicionado)" },
            new DefRisco { chave = "risco_AlagamentoBanheiro_12", titulo = "Alagamento no banheiro", ativo = false,
                risco = "O ralo entupiu e a água tomou conta do piso.",
                seguro = "A cobertura Danos por Água do Seguro Residencial protege contra alagamento. E a Casa em Apuros faz o desentupimento.",
                coberturas = "Danos por água (alagamento) • Casa em apuros (desentupimento)" },
        };

        struct DefTeleporte { public string nome; public Vector3 pos; }

        // Posições iniciais (ajuste livremente na cena depois de gerar).
        static readonly DefTeleporte[] Teleportes =
        {
            new DefTeleporte { nome = "Sala", pos = new Vector3(-2.0f, 0f, -2.2f) },
            new DefTeleporte { nome = "Cozinha", pos = new Vector3(-3.3f, 0f, -4.6f) },
            new DefTeleporte { nome = "Corredor", pos = new Vector3(-5.8f, 0f, -2.6f) },
            new DefTeleporte { nome = "Banheiro", pos = new Vector3(-8.3f, 0f, -4.6f) },
        };

        // ------------------------------------------------------------------ menus

        [MenuItem("BB Seguros/1. Criar dados dos riscos (textos)", false, 1)]
        public static void CriarDados()
        {
            GarantirPasta(PastaDados);
            int criados = 0;
            foreach (var d in Definicoes)
            {
                string caminho = PastaDados + "/RISCO_" + d.chave + ".asset";
                if (AssetDatabase.LoadAssetAtPath<DadosRisco>(caminho) != null) continue;
                var asset = ScriptableObject.CreateInstance<DadosRisco>();
                asset.chaveObjeto = d.chave;
                asset.titulo = d.titulo;
                asset.textoRisco = d.risco;
                asset.textoSeguro = d.seguro;
                asset.coberturas = d.coberturas;
                asset.produto = d.produto;
                asset.ativoNoDesafio = d.ativo;
                asset.ePet = d.pet;
                AssetDatabase.CreateAsset(asset, caminho);
                criados++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[BB Seguros] Dados dos riscos: " + criados + " asset(s) criado(s) em " + PastaDados + " (os já existentes foram mantidos).");
            ImportarTextosCards();
        }

        // ------------------------------------------------------------------ textos dos cards (JSON do Danilo)

        const string CaminhoJsonCards = "Assets/Sprites/cards/textos-cards.json";

        // Número do card no JSON -> objeto da cena.
        static readonly Dictionary<string, string> CardParaObjeto = new Dictionary<string, string>
        {
            { "01", "risco_PrincipioIncendio_03" },
            { "02", "risco_JanelaVulneravel_05" },
            { "03", "risco_Goteira_07" },
            { "04", "risco_AlagamentoBanheiro_12" },
            { "05", "risco_VazamentoGas_04" },
            { "06", "risco_VazamentoAgua_01" },
            { "07", "risco_CurtoCircuito_02" },
            { "08", "risco_ArCondicionado_11" },
            { "09", "risco_AssistenciaDomestica_06" },
            { "10", ChaveCachorro },
            { "11", "risco_PortaArrombada_08" },
            { "12", "risco_LampadaPiscando_09" },
            { "13", "risco_VazamentoChuveiro_10" },
        };

        // Categoria (ponto de descoberta do escopo) de cada objeto. O jogo sorteia 1 por categoria a cada partida.
        static readonly Dictionary<string, CategoriaRisco> CategoriaDoObjeto = new Dictionary<string, CategoriaRisco>
        {
            { "risco_VazamentoAgua_01", CategoriaRisco.VazamentoAgua },
            { "risco_Goteira_07", CategoriaRisco.VazamentoAgua },
            { "risco_AlagamentoBanheiro_12", CategoriaRisco.VazamentoAgua },
            { "risco_VazamentoChuveiro_10", CategoriaRisco.CurtoCircuito }, // card 13: resistência queimada (danos elétricos)
            { "risco_CurtoCircuito_02", CategoriaRisco.CurtoCircuito },
            { "risco_LampadaPiscando_09", CategoriaRisco.CurtoCircuito },
            { "risco_PrincipioIncendio_03", CategoriaRisco.PrincipioIncendio },
            { "risco_VazamentoGas_04", CategoriaRisco.VazamentoGas },
            { "risco_JanelaVulneravel_05", CategoriaRisco.JanelaOuPorta },
            { "risco_PortaArrombada_08", CategoriaRisco.JanelaOuPorta },
            { "risco_AssistenciaDomestica_06", CategoriaRisco.AssistenciaDomestica },
            { "risco_ArCondicionado_11", CategoriaRisco.AssistenciaDomestica },
            { ChaveCachorro, CategoriaRisco.Pet },
        };

#pragma warning disable 649 // preenchidos pelo JsonUtility
        [System.Serializable] class JsonAjuda { public string cobertura; public string texto; }
        [System.Serializable] class JsonCard { public string n; public string nome; public string descricao; public string produto; public JsonAjuda[] ajudas; public bool a_homologar; public bool homologar; }
        [System.Serializable] class JsonCards { public JsonCard[] cards; }
#pragma warning restore 649

        // Copia nome, descrição, produto e coberturas do textos-cards.json para os assets DadosRisco
        // (sobrescreve esses campos; "ativoNoDesafio" e "ePet" são mantidos).
        [MenuItem("BB Seguros/Atualizar textos a partir do textos-cards.json", false, 42)]
        public static void ImportarTextosCards()
        {
            if (!File.Exists(CaminhoJsonCards))
            {
                Debug.LogWarning("[BB Seguros] " + CaminhoJsonCards + " não encontrado; textos dos cards não atualizados.");
                return;
            }
            var json = JsonUtility.FromJson<JsonCards>(File.ReadAllText(CaminhoJsonCards, System.Text.Encoding.UTF8));
            if (json == null || json.cards == null)
            {
                Debug.LogError("[BB Seguros] Não consegui ler " + CaminhoJsonCards + ".");
                return;
            }
            var homologar = new List<string>();
            int atualizados = 0;
            foreach (var c in json.cards)
            {
                if (c == null || string.IsNullOrEmpty(c.n)) continue;
                if (!CardParaObjeto.TryGetValue(c.n, out var chave))
                {
                    Debug.LogWarning("[BB Seguros] Card " + c.n + " (" + c.nome + ") não tem objeto correspondente na cena. Adicione em CardParaObjeto.");
                    continue;
                }
                string caminho = PastaDados + "/RISCO_" + chave + ".asset";
                var d = AssetDatabase.LoadAssetAtPath<DadosRisco>(caminho);
                if (d == null)
                {
                    d = ScriptableObject.CreateInstance<DadosRisco>();
                    d.chaveObjeto = chave;
                    d.ativoNoDesafio = false;
                    AssetDatabase.CreateAsset(d, caminho);
                }
                bool primeiraVez = d.numeroCard != c.n; // asset ainda não tinha recebido este card
                d.numeroCard = c.n;
                d.titulo = c.nome;
                d.textoRisco = c.descricao;
                d.nomeProduto = c.produto;
                d.produto = (c.produto ?? "").Contains("Vida") ? ProdutoSeguro.SeguroVida : ProdutoSeguro.SeguroResidencial;
                d.ajudas = new List<AjudaSeguro>();
                if (c.ajudas != null)
                    foreach (var a in c.ajudas)
                        if (a != null) d.ajudas.Add(new AjudaSeguro { cobertura = a.cobertura, texto = a.texto });
                if (d.ajudas.Count > 2) Debug.LogWarning("[BB Seguros] Card " + c.n + " tem " + d.ajudas.Count + " coberturas; o layout mostra só as 2 primeiras.");
                d.aHomologar = c.a_homologar || c.homologar; // o JSON usa os dois nomes
                // Todo risco que recebe card do Danilo entra no sorteio da sua categoria (só na primeira vez,
                // para não desfazer ajustes manuais nos assets).
                if ((primeiraVez || d.categoria == CategoriaRisco.Nenhuma) && CategoriaDoObjeto.TryGetValue(chave, out var cat))
                {
                    d.categoria = cat;
                    d.ativoNoDesafio = true;
                }
                d.icone = GarantirSprite(PastaVR + "/UI/Icones/icone_" + c.n + ".png", Vector4.zero);
                if (d.aHomologar && d.ativoNoDesafio) homologar.Add(c.n + " " + c.nome);
                EditorUtility.SetDirty(d);
                atualizados++;
            }
            // Riscos sem card do Danilo: recebem a categoria, mas ficam fora do sorteio até ganharem card.
            foreach (var par in CategoriaDoObjeto)
            {
                var d = AssetDatabase.LoadAssetAtPath<DadosRisco>(PastaDados + "/RISCO_" + par.Key + ".asset");
                if (d == null || d.categoria != CategoriaRisco.Nenhuma) continue;
                d.categoria = par.Value;
                EditorUtility.SetDirty(d);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[BB Seguros] Textos dos cards: " + atualizados + " asset(s) atualizados a partir de " + CaminhoJsonCards + "." +
                      (homologar.Count > 0 ? " Ainda a homologar (ativos no desafio): " + string.Join(", ", homologar) + "." : ""));
        }

        [MenuItem("BB Seguros/2. Configurar cena VR (AmbienteBB)", false, 2)]
        public static void ConfigurarCena()
        {
            if (SceneManager.GetActiveScene().path != CaminhoCena)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(CaminhoCena);
            }
            var cena = SceneManager.GetActiveScene();

            // Cada etapa é isolada: se uma falhar, as outras continuam e a cena é salva mesmo assim.
            int riscosLigados = 0;
            Materiais materiais = default;
            Dictionary<string, DadosRisco> dados = null;
            RigVR rig = null;
            PainelVR painel = null;
            GameObject jogadorDesktop = null;
            int falhas = 0;

            Etapa("dados dos riscos", () => { CriarDados(); dados = CarregarDados(); }, ref falhas);
            Etapa("recursos do TextMeshPro", ImportarRecursosTMP, ref falhas);
            Etapa("materiais", () => materiais = GarantirMateriais(), ref falhas);
            Etapa("riscos", () => riscosLigados = ConfigurarRiscos(dados), ref falhas);
            Etapa("cachorro", () => ConfigurarCachorro(dados), ref falhas);
            Etapa("previews de VFX", DesligarPreviewsDeVfx, ref falhas);
            Etapa("rig VR", () =>
            {
                jogadorDesktop = GameObject.Find("Jogador_Teste");
                rig = ConfigurarRig(jogadorDesktop, materiais.linha);
                ConfigurarApontadorDesktop(jogadorDesktop, materiais.linha);
            }, ref falhas);
            Etapa("teleportes", () => ConfigurarTeleportes(materiais.teleporte), ref falhas);
            Etapa("painel", () => painel = ConfigurarPainel(), ref falhas);
            Etapa("gerenciador do jogo", () => ConfigurarJogo(painel, rig, materiais.indicador), ref falhas);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(CaminhoCena, true) };

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            if (falhas == 0)
                Debug.Log("[BB Seguros] Cena configurada: " + riscosLigados + " risco(s) ligados, rig VR, painel, teleportes e gerenciador prontos. Aperte Play para testar no desktop (WASD + mouse, clique = gatilho).");
            else
                Debug.LogWarning("[BB Seguros] Cena salva, mas " + falhas + " etapa(s) falharam (veja os erros acima). Corrija e rode o passo 2 de novo.");
        }

        static void Etapa(string nome, System.Action acao, ref int falhas)
        {
            try { acao(); }
            catch (System.Exception e)
            {
                falhas++;
                Debug.LogError("[BB Seguros] Falha na etapa '" + nome + "': " + e.Message + "\n" + e.StackTrace);
            }
        }

        [MenuItem("BB Seguros/3. Configurar projeto para Meta Quest (Android + OpenXR)", false, 3)]
        public static void ConfigurarQuest()
        {
            ConfigurarPlayerSettingsAndroid();
            ConfigurarXR();
            AssetDatabase.SaveAssets();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (EditorUtility.DisplayDialog("Trocar plataforma para Android?",
                    "As configurações do Quest foram aplicadas. Deseja trocar a plataforma ativa para Android agora? (pode demorar alguns minutos para reimportar os assets)",
                    "Trocar agora", "Depois"))
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Android, BuildTarget.Android);
                }
            }
            Debug.Log("[BB Seguros] Projeto configurado para Meta Quest. Confira em Project Settings > XR Plug-in Management (Android: OpenXR marcado; OpenXR > Meta Quest Support + perfis Oculus Touch / Meta Quest Touch).");
        }

        [MenuItem("BB Seguros/4. Gerar APK para o Quest", false, 4)]
        public static void GerarApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUtility.DisplayDialog("Plataforma", "Troque a plataforma para Android primeiro (menu BB Seguros > 3).", "OK");
                return;
            }
            Directory.CreateDirectory("Builds");
            var opcoes = new BuildPlayerOptions
            {
                scenes = new[] { CaminhoCena },
                locationPathName = "Builds/BBSeguros_GameSeguridade.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            BuildReport relatorio = BuildPipeline.BuildPlayer(opcoes);
            if (relatorio.summary.result == BuildResult.Succeeded)
                Debug.Log("[BB Seguros] APK gerado: " + Path.GetFullPath(opcoes.locationPathName) + "  (instale com: adb install -r <apk>)");
            else
                Debug.LogError("[BB Seguros] Falha ao gerar o APK: " + relatorio.summary.result);
        }

        [MenuItem("BB Seguros/Tudo de uma vez (1 + 2 + 3)", false, 20)]
        public static void Tudo()
        {
            ConfigurarCena();
            ConfigurarQuest();
        }

        [MenuItem("BB Seguros/Importar recursos do TextMeshPro", false, 40)]
        public static void ImportarRecursosTMP()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) return;

            // 1) Importador do próprio TMP (o nome do método mudou entre versões; tentamos os dois).
            var tipo = System.Type.GetType("TMPro.EditorUtilities.TMP_PackageResourceImporter, Unity.TextMeshPro.Editor");
            if (tipo != null)
            {
                var m3 = tipo.GetMethod("ImportResources", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool), typeof(bool), typeof(bool) }, null);
                var m0 = tipo.GetMethod("ImportEssentialResources", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, System.Type.EmptyTypes, null);
                if (m3 != null) m3.Invoke(null, new object[] { true, false, false });
                else if (m0 != null) m0.Invoke(null, null);
                AssetDatabase.Refresh();
                if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
                {
                    Debug.Log("[BB Seguros] Recursos essenciais do TextMeshPro importados.");
                    return;
                }
            }

            // 2) Importa o .unitypackage que vem dentro do pacote ugui.
            string pacote = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
            if (File.Exists(pacote))
            {
                AssetDatabase.ImportPackage(pacote, false);
                AssetDatabase.Refresh();
                Debug.Log("[BB Seguros] Recursos essenciais do TextMeshPro importados a partir do pacote ugui.");
                return;
            }
            Debug.LogWarning("[BB Seguros] Não consegui importar o TMP automaticamente. Use Window > TextMeshPro > Import TMP Essential Resources e rode o passo 2 de novo.");
        }

        // ------------------------------------------------------------------ dados / materiais

        static Dictionary<string, DadosRisco> CarregarDados()
        {
            var mapa = new Dictionary<string, DadosRisco>();
            foreach (var guid in AssetDatabase.FindAssets("t:DadosRisco", new[] { PastaDados }))
            {
                var d = AssetDatabase.LoadAssetAtPath<DadosRisco>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null && !string.IsNullOrEmpty(d.chaveObjeto)) mapa[d.chaveObjeto] = d;
            }
            return mapa;
        }

        struct Materiais { public Material indicador, linha, teleporte; }

        static Materiais GarantirMateriais()
        {
            GarantirPasta(PastaMateriais);
            GarantirPasta(PastaTexturas);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(CaminhoShader);
            if (shader == null)
            {
                Debug.LogWarning("[BB Seguros] Shader " + CaminhoShader + " não encontrado; usando Sprites/Default.");
                shader = Shader.Find("Sprites/Default");
            }

            var texBrilho = GerarTextura(PastaTexturas + "/T_BB_Brilho.png", (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f);
                return a + (r < 0.18f ? 0.6f * (1f - r / 0.18f) : 0f);
            });
            var texAnel = GerarTextura(PastaTexturas + "/T_BB_Anel.png", (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float anel = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.12f);
                float centro = r < 0.7f ? 0.18f * (1f - r / 0.7f) : 0f;
                return r > 0.98f ? 0f : Mathf.Max(anel, centro);
            });

            return new Materiais
            {
                indicador = GarantirMaterial(PastaMateriais + "/M_BB_Indicador.mat", shader, texBrilho),
                linha = GarantirMaterial(PastaMateriais + "/M_BB_Linha.mat", shader, null),
                teleporte = GarantirMaterial(PastaMateriais + "/M_BB_Teleporte.mat", shader, texAnel),
            };
        }

        static Material GarantirMaterial(string caminho, Shader shader, Texture2D textura)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m != null) return m;
            m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (textura != null && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", textura);
            AssetDatabase.CreateAsset(m, caminho);
            return m;
        }

        static Texture2D GerarTextura(string caminho, System.Func<float, float, float> alfa)
        {
            var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
            if (existente != null) return existente;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var cores = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    cores[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alfa(u, v)));
                }
            tex.SetPixels(cores);
            tex.Apply();
            File.WriteAllBytes(caminho, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(caminho);
            var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(caminho);
        }

        // ------------------------------------------------------------------ riscos / cachorro

        static int ConfigurarRiscos(Dictionary<string, DadosRisco> dados)
        {
            Transform raiz = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "BB_Riscos" || t.name == "PRF_Riscos") { raiz = t; break; }
            }
            if (raiz == null)
            {
                Debug.LogError("[BB Seguros] Não encontrei o objeto 'BB_Riscos' (instância de PRF_Riscos) na cena.");
                return 0;
            }
            if (!raiz.gameObject.activeSelf)
            {
                raiz.gameObject.SetActive(true);
                Debug.LogWarning("[BB Seguros] 'BB_Riscos' estava desligado; foi ativado.");
            }

            int ligados = 0;
            foreach (Transform filho in raiz)
            {
                if (!filho.name.StartsWith("risco_")) continue;
                var ponto = filho.GetComponent<PontoDeRisco>();
                if (ponto == null) ponto = Undo.AddComponent<PontoDeRisco>(filho.gameObject);
                ponto.LocalizarGrupos();
                if (dados.TryGetValue(filho.name, out var d)) ponto.dados = d;
                else Debug.LogWarning("[BB Seguros] Sem DadosRisco para " + filho.name + ". Crie um asset com chaveObjeto = '" + filho.name + "'.");
                GarantirColisor(ponto);
                EditorUtility.SetDirty(ponto);
                ligados++;
            }
            return ligados;
        }

        static void GarantirColisor(PontoDeRisco ponto)
        {
            foreach (var c in ponto.GetComponentsInChildren<Collider>(true))
            {
                if (ponto.grupoArrumado != null && c.transform.IsChildOf(ponto.grupoArrumado.transform)) continue;
                return; // já tem algo para o raio acertar
            }
            ponto.CalcularLimites();
            var b = ponto.Limites;
            var esfera = ponto.gameObject.AddComponent<SphereCollider>();
            esfera.isTrigger = true;
            esfera.center = ponto.transform.InverseTransformPoint(b.center);
            esfera.radius = Mathf.Max(0.3f, b.extents.magnitude);
        }

        static void ConfigurarCachorro(Dictionary<string, DadosRisco> dados)
        {
            GameObject cachorro = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.parent == null && t.name.ToLowerInvariant().Contains("cachorro")) { cachorro = t.gameObject; break; }
            }
            if (cachorro == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoPrefabCachorro);
                if (prefab == null)
                {
                    Debug.LogWarning("[BB Seguros] PRF_Cachorro não encontrado; o pet não foi adicionado.");
                    return;
                }
                cachorro = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
                cachorro.name = ChaveCachorro;
                cachorro.transform.position = new Vector3(-2.5f, 0f, -2.0f);
                cachorro.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
                Debug.Log("[BB Seguros] Cachorro adicionado à cena em (-2.5, 0, -2). Ajuste a posição como preferir.");
            }

            var ponto = cachorro.GetComponent<PontoDeRisco>();
            if (ponto == null) ponto = Undo.AddComponent<PontoDeRisco>(cachorro);
            if (dados.TryGetValue(ChaveCachorro, out var d)) ponto.dados = d;
            ponto.deslocamentoIndicador = new Vector3(0f, 0.55f, 0f);
            if (cachorro.GetComponentInChildren<Collider>(true) == null)
            {
                var esfera = cachorro.AddComponent<SphereCollider>();
                esfera.isTrigger = true;
                esfera.center = new Vector3(0f, 0.35f, 0f);
                esfera.radius = 0.45f;
            }
            EditorUtility.SetDirty(ponto);
        }

        static void DesligarPreviewsDeVfx()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.parent != null) continue;
                if (t.name.StartsWith("VFX_Conserto") && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    Debug.Log("[BB Seguros] Preview '" + t.name + "' desligado (o VFX é instanciado pelo jogo ao proteger um risco).");
                }
            }
        }

        // ------------------------------------------------------------------ rig / apontadores / teleportes

        static RigVR ConfigurarRig(GameObject jogadorDesktop, Material materialLinha)
        {
            var goRig = GameObject.Find("XR_Rig");
            if (goRig != null)
            {
                var existente = goRig.GetComponent<RigVR>();
                if (existente != null)
                {
                    if (existente.jogadorDesktop == null) existente.jogadorDesktop = jogadorDesktop;
                    return existente;
                }
            }
            if (goRig == null) goRig = new GameObject("XR_Rig");
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            Camera camDesktop = null;
            if (jogadorDesktop != null)
            {
                pos = jogadorDesktop.transform.position;
                pos.y = Mathf.Max(0f, pos.y);
                rot = Quaternion.Euler(0f, jogadorDesktop.transform.eulerAngles.y, 0f);
                camDesktop = jogadorDesktop.GetComponentInChildren<Camera>(true);
            }
            goRig.transform.SetPositionAndRotation(pos, rot);

            var offset = Filho(goRig.transform, "Camera_Offset");
            var goCam = Filho(offset, "Camera_VR").gameObject;
            var cam = goCam.GetComponent<Camera>();
            if (cam == null) cam = goCam.AddComponent<Camera>();
            if (camDesktop != null) cam.CopyFrom(camDesktop);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
            cam.tag = "MainCamera";
            if (goCam.GetComponent<AudioListener>() == null) goCam.AddComponent<AudioListener>();
            var dadosCam = goCam.GetComponent<UniversalAdditionalCameraData>();
            if (dadosCam == null) dadosCam = goCam.AddComponent<UniversalAdditionalCameraData>();
            dadosCam.renderPostProcessing = false; // pós-processamento pesa demais no Quest
            dadosCam.antialiasing = AntialiasingMode.None;

            var maoEsq = CriarMao(offset, "Mao_Esquerda", ApontadorVR.Fonte.MaoEsquerda, materialLinha);
            var maoDir = CriarMao(offset, "Mao_Direita", ApontadorVR.Fonte.MaoDireita, materialLinha);

            var rig = goRig.GetComponent<RigVR>();
            if (rig == null) rig = goRig.AddComponent<RigVR>();
            rig.deslocamentoCamera = offset;
            rig.cameraVR = cam;
            rig.maoEsquerda = maoEsq;
            rig.maoDireita = maoDir;
            rig.jogadorDesktop = jogadorDesktop;
            EditorUtility.SetDirty(rig);
            return rig;
        }

        static Transform CriarMao(Transform pai, string nome, ApontadorVR.Fonte fonte, Material materialLinha)
        {
            var mao = Filho(pai, nome);
            if (mao.GetComponent<ApontadorVR>() == null)
            {
                var ap = mao.gameObject.AddComponent<ApontadorVR>();
                ap.fonte = fonte;
                ap.materialLinha = materialLinha;
                // Visual simples do controle: um pequeno "bastão" alinhado com o raio.
                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Visual";
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(mao, false);
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localPosition = new Vector3(0f, 0f, -0.03f);
                visual.transform.localScale = new Vector3(0.025f, 0.05f, 0.025f);
            }
            return mao;
        }

        static void ConfigurarApontadorDesktop(GameObject jogadorDesktop, Material materialLinha)
        {
            if (jogadorDesktop == null)
            {
                Debug.LogWarning("[BB Seguros] 'Jogador_Teste' não encontrado: o teste no desktop (sem headset) não vai funcionar.");
                return;
            }
            var cam = jogadorDesktop.GetComponentInChildren<Camera>(true);
            if (cam == null) return;
            if (cam.GetComponent<ApontadorVR>() != null) return;
            var ap = cam.gameObject.AddComponent<ApontadorVR>();
            ap.fonte = ApontadorVR.Fonte.Desktop;
            ap.materialLinha = materialLinha;
            EditorUtility.SetDirty(ap);
        }

        static void ConfigurarTeleportes(Material material)
        {
            var raiz = GameObject.Find("Pontos_De_Teleporte");
            if (raiz != null) return;
            raiz = new GameObject("Pontos_De_Teleporte");
            foreach (var def in Teleportes)
            {
                var go = new GameObject("Teleporte_" + def.nome);
                go.transform.SetParent(raiz.transform, false);
                go.transform.position = def.pos + Vector3.up * 0.015f;
                var ponto = go.AddComponent<PontoDeTeleporte>();
                ponto.nomeAmbiente = def.nome;
                var caixa = go.AddComponent<BoxCollider>();
                caixa.size = new Vector3(0.7f, 0.05f, 0.7f);
                caixa.isTrigger = true;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Disco";
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(go.transform, false);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.localScale = Vector3.one * 0.7f;
                var r = quad.GetComponent<Renderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            Debug.Log("[BB Seguros] " + Teleportes.Length + " pontos de teleporte criados em 'Pontos_De_Teleporte'. Mova-os para os melhores lugares de cada ambiente.");
        }

        // ------------------------------------------------------------------ painel / jogo

        // ------------------------------------------------------------------ painel (cards do Danilo)

        const string PastaUI = PastaVR + "/UI";
        const string CaminhoArredondado = PastaUI + "/ui_arredondado.png";
        const string CaminhoCirculo = PastaUI + "/ui_circulo.png";
        const string CaminhoLogo = PastaUI + "/logo_bbseguros.png";
        const string CaminhoIconeMais = PastaUI + "/Icones/icone_mais.png";
        const string CaminhoIconeCheck = PastaUI + "/Icones/icone_check.png";
        const string CaminhoIconeFinal = PastaUI + "/Icones/icone_final.png";
        const float RaioSprite = 64f; // raio dos cantos no ui_arredondado.png (256 px)

        // Cores amostradas dos PNGs em Assets/Sprites/cards.
        static readonly Color CardFundo = new Color32(0xF7, 0xF8, 0xFF, 0xFF);
        static readonly Color CardMarinho = new Color32(0x1B, 0x24, 0x77, 0xFF);
        static readonly Color CardAzul = new Color32(0x46, 0x5E, 0xFF, 0xFF);
        static readonly Color CardAzulEscuro = new Color32(0x2D, 0x3F, 0xCC, 0xFF);
        static readonly Color CardAmarelo = new Color32(0xFC, 0xFC, 0x30, 0xFF);

        struct SpritesUI { public Sprite arredondado, circulo, logo, mais, check, final; }

        static Sprite GarantirSprite(string caminho, Vector4 borda)
        {
            if (!File.Exists(caminho))
            {
                Debug.LogWarning("[BB Seguros] Imagem não encontrada: " + caminho);
                return null;
            }
            AssetDatabase.ImportAsset(caminho);
            var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spriteBorder != borda || !imp.mipmapEnabled))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = 100f;
                imp.spriteBorder = borda;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = true; // texto e ícones nítidos à distância no headset
                imp.filterMode = FilterMode.Trilinear;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        }

        static SpritesUI CarregarSpritesUI()
        {
            var b = new Vector4(RaioSprite, RaioSprite, RaioSprite, RaioSprite);
            return new SpritesUI
            {
                arredondado = GarantirSprite(CaminhoArredondado, b),
                circulo = GarantirSprite(CaminhoCirculo, Vector4.zero),
                logo = GarantirSprite(CaminhoLogo, Vector4.zero),
                mais = GarantirSprite(CaminhoIconeMais, Vector4.zero),
                check = GarantirSprite(CaminhoIconeCheck, Vector4.zero),
                final = GarantirSprite(CaminhoIconeFinal, Vector4.zero),
            };
        }

        [MenuItem("BB Seguros/Recriar painel (cards)", false, 41)]
        public static void RecriarPainel()
        {
            var existente = GameObject.Find("Painel_VR");
            if (existente != null) Undo.DestroyObjectImmediate(existente);
            ConfigurarCena();
        }

        static PainelVR ConfigurarPainel()
        {
            var existente = GameObject.Find("Painel_VR");
            if (existente != null)
            {
                var p = existente.GetComponent<PainelVR>();
                if (p != null && p.cartaoRisco != null && p.cartaoProtegido != null && p.cartaoFinal != null && p.chip != null) return p;
                // Painel de uma versão anterior (antes dos cards do Danilo) ou incompleto: recria do zero.
                Object.DestroyImmediate(existente);
            }
            var s = CarregarSpritesUI();
            var fonte = ObterFonteTMP();

            var go = new GameObject("Painel_VR", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            var grupo = go.AddComponent<CanvasGroup>();
            var raiz = go.GetComponent<RectTransform>();
            raiz.sizeDelta = new Vector2(PainelVR.LarguraDesign, 900f);
            raiz.localScale = Vector3.one * (2f * 1.5f * Mathf.Tan(12.5f * Mathf.Deg2Rad) / PainelVR.LarguraDesign);
            var painel = go.AddComponent<PainelVR>();
            painel.grupo = grupo;

            // ---------------- card do risco
            {
                var card = NovoCartao(raiz, "Card_Risco", s);
                var cab = Secao(card, "Cabecalho", true, 64, 64, 64, 0, 40, TextAnchor.MiddleLeft);
                painel.riscoIcone = Icone(cab, "Icone", s.mais, 144f);
                var col = Coluna(cab, "Coluna", 12f, TextAnchor.UpperLeft);
                painel.riscoEtiqueta = Pilula(col, "Etiqueta", "Risco identificado", CardMarinho, Color.white, 40f, s.arredondado, fonte);
                painel.riscoTitulo = TextoLayout(col, "Titulo", "Nome do risco", 80f, FontStyles.Bold, CardMarinho, TextAlignmentOptions.TopLeft, fonte);
                var corpo = Secao(card, "Corpo", false, 64, 64, 32, 0, 0, TextAnchor.UpperLeft);
                painel.riscoDescricao = TextoLayout(corpo, "Descricao", "Uma frase curta descrevendo o que está fora do lugar.", 46f, FontStyles.Normal, CardMarinho, TextAlignmentOptions.TopLeft, fonte);
                RectTransform fill;
                painel.riscoBotao = Rodape(card, "Consertar", 480f, s, fonte, out fill);
                painel.riscoPreenchimento = fill;
                painel.riscoDica = Dica(card, "Segure o gatilho para consertar", fonte);
                painel.cartaoRisco = card.gameObject;
            }

            // ---------------- card de proteção
            {
                var card = NovoCartao(raiz, "Card_Protegido", s);
                var topo = Secao(card, "Topo", true, 64, 64, 56, 56, 40, TextAnchor.MiddleLeft);
                FundoTopo(topo, s);
                Icone(topo, "Icone", s.check, 144f);
                var col = Coluna(topo, "Coluna", 4f, TextAnchor.UpperLeft);
                painel.protTitulo = TextoLayout(col, "Titulo", "Muito bem!", 80f, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft, fonte);
                painel.protSubtitulo = TextoLayout(col, "Subtitulo", "Não se preocupe, garantimos a proteção completa.", 46f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft, fonte);

                var corpo = Secao(card, "Corpo", false, 64, 64, 52, 0, 28, TextAnchor.UpperLeft);
                painel.protComoAjuda = TextoLayout(corpo, "ComoAjuda", "Como o Seguro Residencial ajuda", 46f, FontStyles.Bold, CardAzulEscuro, TextAlignmentOptions.TopLeft, fonte);
                painel.protItens = new GameObject[2];
                painel.protCoberturas = new TMP_Text[2];
                painel.protTextos = new TMP_Text[2];
                for (int i = 0; i < 2; i++)
                {
                    var item = Coluna(corpo, "Cobertura_" + (i + 1), 12f, TextAnchor.UpperLeft);
                    painel.protCoberturas[i] = Pilula(item, "Etiqueta", "Cobertura " + (i + 1), CardAmarelo, CardMarinho, 42f, s.arredondado, fonte);
                    painel.protTextos[i] = TextoLayout(item, "Texto", "Uma frase curta sobre como ajuda.", 46f, FontStyles.Normal, CardMarinho, TextAlignmentOptions.TopLeft, fonte);
                    painel.protItens[i] = item.gameObject;
                }
                painel.protNota = TextoLayout(corpo, "Nota", "*Consulte condições gerais", 34f, FontStyles.Normal, CardAzulEscuro, TextAlignmentOptions.TopLeft, fonte);
                RectTransform fill;
                painel.protBotao = Rodape(card, "Continuar", 480f, s, fonte, out fill);
                painel.protDica = Dica(card, "Aperte o gatilho para continuar", fonte);
                painel.cartaoProtegido = card.gameObject;
                card.gameObject.SetActive(false);
            }

            // ---------------- card final
            {
                var card = NovoCartao(raiz, "Card_Final", s);
                var topo = Secao(card, "Topo", false, 64, 64, 64, 56, 24, TextAnchor.UpperCenter);
                FundoTopo(topo, s);
                Icone(topo, "Icone", s.final, 160f);
                painel.finalTitulo = TextoLayout(topo, "Titulo", "Você encontrou os riscos!", 96f, FontStyles.Bold, Color.white, TextAlignmentOptions.Top, fonte);
                painel.finalSubtitulo = TextoLayout(topo, "Subtitulo", "A BB Seguros ajuda a proteger tudo o que importa.", 50f, FontStyles.Normal, Color.white, TextAlignmentOptions.Top, fonte);

                var corpo = Secao(card, "Corpo", false, 64, 64, 56, 0, 32, TextAnchor.UpperLeft);
                painel.finalPasso1 = Passo(corpo, "1", "Pode retirar os óculos.", s, fonte);
                painel.finalPasso2 = Passo(corpo, "2", "No estande, aponte o celular para o QR Code e conheça as coberturas.", s, fonte);
                painel.finalResumo = TextoLayout(corpo, "Resumo", "7 de 7 encontrados  •  Tempo: 2:30", 36f, FontStyles.Normal, CardAzulEscuro, TextAlignmentOptions.TopLeft, fonte);
                RectTransform fill;
                painel.finalBotao = Rodape(card, "Jogar novamente", 600f, s, fonte, out fill);
                painel.finalDica = Dica(card, "Aperte o gatilho para jogar novamente", fonte);
                painel.cartaoFinal = card.gameObject;
                card.gameObject.SetActive(false);
            }

            // ---------------- chip (contador durante a exploração)
            {
                var chip = CriarImagem(raiz, "Chip", CardFundo, s.arredondado, Image.Type.Sliced);
                chip.pixelsPerUnitMultiplier = RaioSprite / 55f;
                chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                chip.rectTransform.sizeDelta = new Vector2(600f, 110f);
                var bola = CriarImagem(chip.rectTransform, "Marcador", CardAmarelo, s.circulo, Image.Type.Simple);
                bola.rectTransform.anchorMin = bola.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                bola.rectTransform.sizeDelta = new Vector2(56f, 56f);
                bola.rectTransform.anchoredPosition = new Vector2(55f, 0f);
                var txt = CriarTexto(chip.rectTransform, "Texto", fonte, 50f, FontStyles.Bold, CardMarinho, TextAlignmentOptions.Center);
                Esticar(txt.rectTransform, Vector2.zero, Vector2.one, new Vector2(95f, 0f), new Vector2(-30f, 0f));
                txt.text = "0 de 7 encontrados";
                chip.gameObject.SetActive(false);
                painel.chip = chip.gameObject;
                painel.chipTexto = txt;
            }

            foreach (var c in new[] { painel.cartaoRisco, painel.cartaoProtegido, painel.cartaoFinal })
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)c.transform);
            EditorUtility.SetDirty(painel);
            return painel;
        }

        // Card: layout vertical que cresce com o conteúdo, fundo arredondado claro.
        static RectTransform NovoCartao(RectTransform raiz, string nome, SpritesUI s)
        {
            var rt = NovoRect(raiz, nome);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PainelVR.LarguraDesign, 700f);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var f = rt.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var fundo = CriarImagem(rt, "Fundo", CardFundo, s.arredondado, Image.Type.Sliced);
            Esticar(fundo.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ForaDoLayout(fundo.gameObject);
            return rt;
        }

        static RectTransform Secao(RectTransform pai, string nome, bool horizontal, int esq, int dir, int topo, int base_, float espaco, TextAnchor alinhamento)
        {
            var rt = NovoRect(pai, nome);
            HorizontalOrVerticalLayoutGroup g = horizontal
                ? (HorizontalOrVerticalLayoutGroup)rt.gameObject.AddComponent<HorizontalLayoutGroup>()
                : rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.padding = new RectOffset(esq, dir, topo, base_);
            g.spacing = espaco;
            g.childAlignment = alinhamento;
            g.childControlWidth = g.childControlHeight = true;
            g.childForceExpandWidth = g.childForceExpandHeight = false;
            return rt;
        }

        static RectTransform Coluna(RectTransform pai, string nome, float espaco, TextAnchor alinhamento)
        {
            var rt = Secao(pai, nome, false, 0, 0, 0, 0, espaco, alinhamento);
            rt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            return rt;
        }

        // Faixa azul do topo: retângulo arredondado + metade de baixo reta (os cantos de baixo ficam retos).
        static void FundoTopo(RectTransform topo, SpritesUI s)
        {
            var a = CriarImagem(topo, "Fundo_Azul", CardAzul, s.arredondado, Image.Type.Sliced);
            Esticar(a.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ForaDoLayout(a.gameObject);
            var b = CriarImagem(topo, "Fundo_Azul_Base", CardAzul, null, Image.Type.Simple);
            Esticar(b.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            ForaDoLayout(b.gameObject);
            a.transform.SetAsFirstSibling();
            b.transform.SetSiblingIndex(1);
        }

        static Image Icone(RectTransform pai, string nome, Sprite sprite, float tamanho)
        {
            var img = CriarImagem(pai, nome, Color.white, sprite, Image.Type.Simple);
            img.preserveAspect = true;
            var el = img.gameObject.AddComponent<LayoutElement>();
            el.minWidth = el.preferredWidth = tamanho;
            el.minHeight = el.preferredHeight = tamanho;
            return img;
        }

        static TextMeshProUGUI TextoLayout(RectTransform pai, string nome, string exemplo, float tamanho, FontStyles estilo, Color cor, TextAlignmentOptions alinhamento, TMP_FontAsset fonte)
        {
            var t = CriarTexto(pai, nome, fonte, tamanho, estilo, cor, alinhamento);
            t.text = exemplo;
            t.textWrappingMode = TextWrappingModes.Normal;
            if (tamanho >= 70f) t.lineSpacing = -12f;
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            return t;
        }

        // Etiqueta em pílula que se ajusta ao tamanho do texto.
        static TextMeshProUGUI Pilula(RectTransform pai, string nome, string exemplo, Color fundo, Color corTexto, float tamanho, Sprite arredondado, TMP_FontAsset fonte)
        {
            var img = CriarImagem(pai, nome, fundo, arredondado, Image.Type.Sliced);
            float altura = tamanho + 36f;
            img.pixelsPerUnitMultiplier = RaioSprite / (altura * 0.5f);
            var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(30, 30, 14, 18);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true; // texto ocupa a pílula toda e fica centralizado
            var t = CriarTexto(img.rectTransform, "Texto", fonte, tamanho, FontStyles.Bold, corTexto, TextAlignmentOptions.Center);
            t.text = exemplo;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        // Rodapé: logo BB Seguros à esquerda e botão à direita. Devolve o texto do botão e a máscara de preenchimento.
        static TextMeshProUGUI Rodape(RectTransform card, string rotulo, float larguraBotao, SpritesUI s, TMP_FontAsset fonte, out RectTransform preenchimento)
        {
            var rod = Secao(card, "Rodape", true, 64, 64, 48, 0, 24, TextAnchor.MiddleLeft);
            var logo = CriarImagem(rod, "Logo_BBSeguros", CardAzul, s.logo, Image.Type.Simple);
            logo.preserveAspect = true;
            var el = logo.gameObject.AddComponent<LayoutElement>();
            el.preferredWidth = 322f;
            el.preferredHeight = 60f;
            NovoRect(rod, "Espaco").gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            const float altura = 170f;
            var botao = NovoRect(rod, "Botao");
            var eb = botao.gameObject.AddComponent<LayoutElement>();
            eb.minWidth = eb.preferredWidth = larguraBotao;
            eb.minHeight = eb.preferredHeight = altura;
            float mult = RaioSprite / (altura * 0.5f);

            var sombra = CriarImagem(botao, "Sombra", CardAzulEscuro, s.arredondado, Image.Type.Sliced);
            sombra.pixelsPerUnitMultiplier = mult;
            Esticar(sombra.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -10f), new Vector2(0f, -10f));
            var face = CriarImagem(botao, "Face", CardAzul, s.arredondado, Image.Type.Sliced);
            face.pixelsPerUnitMultiplier = mult;
            Esticar(face.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Preenchimento do "segurar para consertar": máscara que cresce da esquerda para a direita.
            var mascara = NovoRect(botao, "Preenchimento");
            mascara.gameObject.AddComponent<RectMask2D>();
            mascara.anchorMin = new Vector2(0f, 0f);
            mascara.anchorMax = new Vector2(0f, 1f);
            mascara.pivot = new Vector2(0f, 0.5f);
            mascara.sizeDelta = new Vector2(0f, 0f);
            var cheio = CriarImagem(mascara, "Cheio", CardAzulEscuro, s.arredondado, Image.Type.Sliced);
            cheio.pixelsPerUnitMultiplier = mult;
            cheio.rectTransform.anchorMin = new Vector2(0f, 0f);
            cheio.rectTransform.anchorMax = new Vector2(0f, 1f);
            cheio.rectTransform.pivot = new Vector2(0f, 0.5f);
            cheio.rectTransform.sizeDelta = new Vector2(larguraBotao, 0f);

            var t = CriarTexto(botao, "Texto", fonte, 64f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            Esticar(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
            t.text = rotulo;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMin = 40f;
            t.fontSizeMax = 64f;
            preenchimento = mascara;
            return t;
        }

        static TextMeshProUGUI Dica(RectTransform card, string exemplo, TMP_FontAsset fonte)
        {
            var sec = Secao(card, "Dica", false, 64, 64, 28, 44, 0, TextAnchor.UpperCenter);
            return TextoLayout(sec, "Texto", exemplo, 34f, FontStyles.Bold, CardAzulEscuro, TextAlignmentOptions.Top, fonte);
        }

        static TextMeshProUGUI Passo(RectTransform pai, string numero, string exemplo, SpritesUI s, TMP_FontAsset fonte)
        {
            var linha = Secao(pai, "Passo_" + numero, true, 0, 0, 0, 0, 36, TextAnchor.MiddleLeft);
            linha.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var bola = Icone(linha, "Numero", s.circulo, 96f);
            bola.color = CardAmarelo;
            var n = CriarTexto(bola.rectTransform, "Texto", fonte, 48f, FontStyles.Bold, CardMarinho, TextAlignmentOptions.Center);
            Esticar(n.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            n.text = numero;
            return TextoLayout(linha, "Texto", exemplo, 52f, FontStyles.Bold, CardMarinho, TextAlignmentOptions.MidlineLeft, fonte);
        }

        static RectTransform NovoRect(Transform pai, string nome)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);
            return go.GetComponent<RectTransform>();
        }

        static void ForaDoLayout(GameObject go)
        {
            var el = go.AddComponent<LayoutElement>();
            el.ignoreLayout = true;
        }

        static TMP_FontAsset ObterFonteTMP()
        {
            TMP_FontAsset f = null;
            if (TMP_Settings.instance != null) f = TMP_Settings.defaultFontAsset; // o getter quebra se o TMP Settings não existir
            if (f != null) return f;
            f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (f == null) Debug.LogWarning("[BB Seguros] Fonte do TextMeshPro não encontrada. Importe os recursos essenciais (Window > TextMeshPro) e rode o passo 2 de novo.");
            return f;
        }

        static Image CriarImagem(Transform pai, string nome, Color cor, Sprite sprite, Image.Type tipo)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);
            var img = go.AddComponent<Image>();
            img.color = cor;
            img.sprite = sprite;
            img.type = tipo;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI CriarTexto(Transform pai, string nome, TMP_FontAsset fonte, float tamanho, FontStyles estilo, Color cor, TextAlignmentOptions alinhamento)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (fonte != null) t.font = fonte;
            t.fontSize = tamanho;
            t.fontStyle = estilo;
            t.color = cor;
            t.alignment = alinhamento;
            t.raycastTarget = false;
            t.text = nome;
            return t;
        }

        static void Esticar(RectTransform rt, Vector2 ancMin, Vector2 ancMax, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = ancMin;
            rt.anchorMax = ancMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        static void ConfigurarJogo(PainelVR painel, RigVR rig, Material materialIndicador)
        {
            var go = GameObject.Find("Jogo_Riscos");
            if (go == null) go = new GameObject("Jogo_Riscos");
            var jogo = go.GetComponent<JogoRiscos>();
            if (jogo == null) jogo = go.AddComponent<JogoRiscos>();
            if (jogo.painel == null) jogo.painel = painel;
            if (jogo.rig == null) jogo.rig = rig;
            if (jogo.materialIndicador == null) jogo.materialIndicador = materialIndicador;
            jogo.corIndicadorHover = AmareloBB;
            if (jogo.vfxProtecao == null) jogo.vfxProtecao = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoVfxConserto);
            if (jogo.iconeIntro == null) jogo.iconeIntro = GarantirSprite(CaminhoIconeMais, Vector4.zero);
            if (jogo.fonteAudio == null)
            {
                var audio = go.GetComponent<AudioSource>();
                if (audio == null) audio = go.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 0f;
                jogo.fonteAudio = audio;
            }
            EditorUtility.SetDirty(jogo);
        }

        // ------------------------------------------------------------------ Quest

        static void ConfigurarPlayerSettingsAndroid()
        {
            var alvo = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Dilis";
            PlayerSettings.productName = "BB Seguros - Game Seguridade";
            PlayerSettings.SetApplicationIdentifier(alvo, "br.com.dilis.bbseguros.gameseguridade");
            PlayerSettings.SetScriptingBackend(alvo, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.androidTVCompatibility = false;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            Debug.Log("[BB Seguros] Player Settings Android: IL2CPP, ARM64, minSdk 32, Vulkan, ASTC, Linear.");
        }

        static void ConfigurarXR()
        {
#if BB_XR_MANAGEMENT
            ConfigurarLoaderOpenXR(BuildTargetGroup.Android);
            ConfigurarLoaderOpenXR(BuildTargetGroup.Standalone); // para testar via Quest Link no editor
#else
            Debug.LogWarning("[BB Seguros] Pacote XR Plug-in Management não encontrado. Confira o Packages/manifest.json.");
#endif
#if BB_OPENXR
            ConfigurarFeaturesOpenXR(BuildTargetGroup.Android,
                "com.unity.openxr.feature.metaquest",
                "com.unity.openxr.feature.input.oculustouch",
                "com.unity.openxr.feature.input.metaquestplus",
                "com.unity.openxr.feature.input.metaquestpro");
            ConfigurarFeaturesOpenXR(BuildTargetGroup.Standalone,
                "com.unity.openxr.feature.input.oculustouch",
                "com.unity.openxr.feature.input.metaquestplus",
                "com.unity.openxr.feature.input.metaquestpro");
#else
            Debug.LogWarning("[BB Seguros] Pacote OpenXR não encontrado. Confira o Packages/manifest.json.");
#endif
        }

#if BB_XR_MANAGEMENT
        static void ConfigurarLoaderOpenXR(BuildTargetGroup grupo)
        {
            XRGeneralSettingsPerBuildTarget geral;
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out geral);
            if (geral == null)
            {
                GarantirPasta("Assets/XR");
                geral = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(geral, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, geral, true);
            }
            XRGeneralSettings cfg = geral.SettingsForBuildTarget(grupo);
            if (cfg == null)
            {
                var metodo = geral.GetType().GetMethod("CreateDefaultManagerSettingsForBuildTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (metodo != null) metodo.Invoke(geral, new object[] { grupo });
                cfg = geral.SettingsForBuildTarget(grupo);
            }
            if (cfg == null || cfg.Manager == null)
            {
                Debug.LogWarning("[BB Seguros] Não consegui criar as configurações XR para " + grupo + ". Abra Project Settings > XR Plug-in Management, marque OpenXR em " + grupo + " e rode o passo 3 de novo.");
                SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
                return;
            }
            cfg.InitManagerOnStart = true;
            bool ok = XRPackageMetadataStore.AssignLoader(cfg.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", grupo);
            EditorUtility.SetDirty(cfg);
            EditorUtility.SetDirty(cfg.Manager);
            EditorUtility.SetDirty(geral);
            Debug.Log("[BB Seguros] Loader OpenXR " + (ok ? "ativado" : "já ativo ou indisponível") + " para " + grupo + ".");
        }
#endif

#if BB_OPENXR
        static void ConfigurarFeaturesOpenXR(BuildTargetGroup grupo, params string[] ids)
        {
            FeatureHelpers.RefreshFeatures(grupo);
            foreach (var id in ids)
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(grupo, id);
                if (feature == null)
                {
                    Debug.LogWarning("[BB Seguros] Feature OpenXR '" + id + "' não encontrada para " + grupo + " (versão do pacote pode não ter esse perfil).");
                    continue;
                }
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
            var cfg = OpenXRSettings.GetSettingsForBuildTargetGroup(grupo);
            if (cfg != null)
            {
                cfg.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                EditorUtility.SetDirty(cfg);
            }
        }
#endif

        // ------------------------------------------------------------------ utilidades

        static Transform Filho(Transform pai, string nome)
        {
            var t = pai.Find(nome);
            if (t != null) return t;
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            return go.transform;
        }

        static void GarantirPasta(string caminho)
        {
            if (AssetDatabase.IsValidFolder(caminho)) return;
            string pai = Path.GetDirectoryName(caminho).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(pai)) GarantirPasta(pai);
            AssetDatabase.CreateFolder(pai, Path.GetFileName(caminho));
        }
    }
}
