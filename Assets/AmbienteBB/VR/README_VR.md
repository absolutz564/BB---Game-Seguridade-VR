# BB Seguros – Game Seguridade (VR / Meta Quest)

Experiência "Encontre os riscos. Ative a proteção." em realidade virtual, montada sobre a cena `AmbienteBB`.
Referência de tom e cores: "BB Play na proteção" (https://bb-play-exp.web.app/).

## Como montar (uma vez)

1. Abra o projeto e aguarde o Package Manager instalar `com.unity.xr.openxr`, `com.unity.xr.management` e `com.unity.xr.core-utils` (já estão no `Packages/manifest.json`).
2. Menu **BB Seguros > 1. Criar dados dos riscos (textos)** – cria um asset `DadosRisco` por risco em `Assets/AmbienteBB/VR/Dados`. Os textos são editáveis ali (homologação de Produto/Marketing/Jurídico).
3. Menu **BB Seguros > 2. Configurar cena VR (AmbienteBB)** – liga cada `risco_*` ao seu asset, cria o `XR_Rig`, o `Painel_VR`, os pontos de teleporte, o `Jogo_Riscos`, coloca o cachorro e importa os recursos do TextMeshPro. Pode rodar de novo à vontade (não duplica nada).
4. Menu **BB Seguros > 3. Configurar projeto para Meta Quest** – Player Settings Android (IL2CPP, ARM64, minSdk 32, Vulkan, ASTC) e XR Plug-in Management com OpenXR + Meta Quest Support + perfis de controle. Aceite a troca de plataforma para Android.
5. Menu **BB Seguros > 4. Gerar APK para o Quest** – gera `Builds/BBSeguros_GameSeguridade.apk`. Instale com `adb install -r Builds/BBSeguros_GameSeguridade.apk` (modo desenvolvedor ligado no headset).

Teste sem headset: aperte Play. O `Jogador_Teste` assume (WASD + mouse; clique esquerdo ou espaço = gatilho; R segurado = reiniciar).
Teste com headset no editor: Quest Link ligado antes do Play (o loader OpenXR também fica ativo no Standalone).

## Cards (design do Danilo)

Os cards seguem os modelos em `Assets/Sprites/cards`, mas são montados com elementos de UI (não com os PNGs, que têm texto sobreposto):

- **Card do risco**: ícone amarelo, etiqueta "Risco identificado", nome, descrição, logo e botão **Consertar** (segurar o gatilho enche o botão).
- **Card de proteção**: faixa azul "Muito bem!", "Como o {produto} ajuda", até 2 coberturas em etiquetas amarelas, "*Consulte condições gerais" e botão **Continuar**.
- **Card final**: "Você encontrou os riscos!", passos 1 e 2, contagem e tempo, botão **Jogar novamente**.
- Durante a exploração fica só o chip "N de 7 encontrados".

Textos dos riscos: `Assets/Sprites/cards/textos-cards.json`. O passo 2 (ou o menu **BB Seguros > Atualizar textos a partir do textos-cards.json**) copia esses textos para os assets em `Dados`. Textos de interface (introdução, botões, dicas, card final) ficam em `Jogo_Riscos > textos`.
Ícones e logo extraídos dos PNGs: `Assets/AmbienteBB/VR/UI`. Tamanho: `Painel_VR > larguraGraus` (25°) e `distancia` (1,5 m). Para refazer o painel do zero: **BB Seguros > Recriar painel (cards)**.

## Mecânica (decisão de interação)

Na versão mobile de referência o usuário **arrasta** um botão para "acionar o seguro". Em VR, arrastar com o controle exige precisão e cansa em fila de evento, então a equivalência adotada é:

| Etapa | Mobile (referência) | VR (este projeto) |
|---|---|---|
| Encontrar | toque no objeto | apontar o controle (raio) e apertar o gatilho |
| Acionar o seguro | "Arraste para acionar" | **segurar o gatilho** ~1 s com barra de progresso ("Segure o gatilho para acionar!" / "Acionando...") |
| Feedback | "Muito Bem!" | "Muito bem!" + "Como o seguro ajuda" + cenografia troca de *Risco* para *Arrumado* + partículas + vibração. Fica na tela até apertar o gatilho (sem tempo limite) |
| Mover-se | toque e arraste | analógico esquerdo anda (para onde a cabeça aponta, sem atravessar paredes), analógico direito gira 45°, e teleporte apontando para os discos no chão |

Qualquer um dos dois controles funciona. O pet (cachorro) só fica interativo depois dos outros seis riscos, como "elemento-surpresa", e fecha a experiência.

Fluxo: Introdução (gatilho para começar) → Explorando (o cartão vira o chip "N de 7" ao apertar o gatilho) → Mensagem ("Risco identificado"; segurar para acionar) → Feedback ("Como o seguro ajuda"; gatilho para continuar; após o 6º risco vem o cartão do pet, também fechado com o gatilho) → … → "Desafio concluído!" com contagem "N de 7" e tempo. Reinício: automático após 40 s, ou botão Menu do controle esquerdo / tecla R segurados por 1,5 s.

## Objetos criados na cena

- `XR_Rig` (`RigVR`): câmera VR + mãos com `ApontadorVR`. Rastreamento via Input System (`<XRHMD>`, `<XRController>{…}/pointerPosition`). Sem headset, desliga a si mesmo e liga o `Jogador_Teste`.
- `Painel_VR` (`PainelVR`): canvas em espaço de mundo que acompanha o olhar com suavização, inclusive para baixo e para cima (inclinação limitada para nunca sair da visão). O cartão cresce com o texto (layout vertical). Ajustes: `distancia` (1,1 m), `alturaCartao`, `alturaChip`, `inclinacaoMaximaBaixo/Cima`.
- `Pontos_De_Teleporte`: 4 discos (Sala, Cozinha, Corredor, Banheiro). Mova-os para os melhores pontos de observação.
- `Jogo_Riscos` (`JogoRiscos`): estado do desafio, textos de interface, sons (campos opcionais) e tempos.
- Em cada `risco_*`: `PontoDeRisco` com o asset de dados; grupos `Risco`/`Arrumado` ligados automaticamente.
- `Cachorro`: instância de `PRF_Cachorro` com `PontoDeRisco` (pet). Ajuste a posição.

## Sorteio dos 7 pontos de descoberta

Cada asset em `Dados` tem uma **categoria** (vazamento de água, curto-circuito, princípio de incêndio, vazamento de gás, janela ou porta, assistência doméstica, pet). A cada partida o `Jogo_Riscos` sorteia **1 risco por categoria** entre os que têm `ativoNoDesafio` ligado. Os não sorteados aparecem consertados. O console mostra o que foi sorteado. Para desligar o sorteio: `Jogo_Riscos > sortearPorCategoria`.

Candidatos hoje (13 cards do Danilo): vazamento de água = pia, goteira ou alagamento; curto-circuito = tomada, lâmpada piscando ou chuveiro com defeito; janela ou porta = janela ou porta arrombada; assistência doméstica = insetos ou ar-condicionado; incêndio, gás e pet têm um risco só.

## Riscos ativos no desafio (7) (referência anterior ao sorteio)

| Objeto | Título | Produto / cobertura citada |
|---|---|---|
| risco_VazamentoAgua_01 | Vazamento de água | Danos por água • Casa em apuros (encanador) |
| risco_CurtoCircuito_02 | Curto-circuito | Danos elétricos • Casa em apuros (eletricista) |
| risco_PrincipioIncendio_03 | Princípio de incêndio | Incêndio |
| risco_VazamentoGas_04 | Vazamento de gás | Casa segura (manutenção de aquecedor a gás) • Incêndio |
| risco_JanelaVulneravel_05 | Janela vulnerável | Casa segura (tela, película) • Casa em apuros (chaveiro) |
| risco_AssistenciaDomestica_06 | Assistência doméstica | Casa sem insetos • Casa limpa • Casa funcionando |
| Cachorro | Alguém da família | Seguro de Vida: consulta veterinária • hospedagem pet |

Os outros seis riscos modelados (goteira, porta arrombada, lâmpada, chuveiro, ar-condicionado, alagamento) têm asset com `ativoNoDesafio = false`. Para usar em outro evento, basta ligar o campo no asset.

Todos os textos partem do "Material Convenção BB Seguros" e **precisam de homologação** antes da versão final.

## Pendências para o evento

- Sons (campos `somInicio`, `somIdentificar`, `somProteger`, `somPet`, `somConclusao` em `Jogo_Riscos`).
- Posição final dos discos de teleporte e do cachorro; validar que nenhum risco fica fora de alcance (raio = 12 m).
- Qualidade/URP para Quest: usar `Mobile_RPAsset` no nível de qualidade do Android; luzes em tempo real ao mínimo.
- Testar a duração (2 a 4 min) com pessoas que nunca usaram VR.
- QR Code do stand (fora do app): o painel final só orienta a tirar o óculos.
