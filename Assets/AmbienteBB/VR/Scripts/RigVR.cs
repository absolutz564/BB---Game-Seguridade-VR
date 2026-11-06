using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace AmbienteBB.VR
{
    // Raiz do jogador em VR. Liga o rastreamento de cabeça/controles via Input System + OpenXR,
    // anda pelo analógico esquerdo (na direção do olhar, sem atravessar paredes), gira em passos (snap turn)
    // pelo analógico direito e faz teleporte. Se nenhum headset estiver ativo, cai no modo desktop
    // (Jogador_Teste com teclado/mouse) para testar a mecânica no editor.
    public class RigVR : MonoBehaviour
    {
        [Header("Referências")]
        public Transform deslocamentoCamera;
        public Camera cameraVR;
        public Transform maoEsquerda;
        public Transform maoDireita;
        [Tooltip("Jogador de teste (teclado/mouse). Fica ativo só quando não há headset.")]
        public GameObject jogadorDesktop;

        [Header("Andar pelo analógico esquerdo")]
        [Tooltip("Desligado: os dois analógicos só giram (como antes) e o deslocamento é só por teleporte.")]
        public bool movimentoContinuo = true;
        [Tooltip("Velocidade máxima em m/s. Abaixo de 2 m/s é mais confortável para quem nunca usou VR.")]
        public float velocidade = 1.5f;
        [Range(0.05f, 0.5f)] public float zonaMortaMovimento = 0.15f;
        [Tooltip("Raio do corpo para não atravessar paredes e móveis.")]
        public float raioCorpo = 0.2f;
        public LayerMask camadasColisao = ~0;

        [Header("Limites da casa")]
        [Tooltip("Impede que a cabeça atravesse paredes, portas e janelas, seja pelo analógico ou andando de verdade no espaço físico.")]
        public bool impedirAtravessarParedes = true;
        [Tooltip("Se a pessoa for parar fora da casa (sem teto acima da cabeça), volta sozinha para o último ponto dentro. " +
                 "Desliga sozinho se o ponto de início não tiver teto com colisor.")]
        public bool voltarSeSairDaCasa = true;
        [Tooltip("Até que altura acima da cabeça procurar o teto, em metros.")]
        public float alturaMaximaTeto = 8f;
        [Tooltip("Segundos fora da casa antes de voltar sozinho.")]
        public float tempoForaAntesDeVoltar = 0.5f;

        [Header("Giro em passos (analógico direito)")]
        public float anguloGiro = 45f;
        [Range(0.3f, 0.95f)] public float zonaMorta = 0.7f;

        public bool ModoVR { get; private set; }

        InputAction acaoGiroEsq;
        InputAction acaoGiroDir;
        bool giroEmCurso;
        readonly List<InputAction> acoes = new List<InputAction>();

        Vector3 ultimaCabecaValida;
        bool temCabecaValida;
        bool tetoVerificado;
        bool tetoDisponivel;
        float tempoFora;

        void Awake()
        {
            // Paredes e vidros finos costumam ter colisor de uma face só: sem isso, o raio "vê" a parede de um lado
            // e atravessa do outro (foi assim que deu para sair da casa e não conseguir voltar).
            Physics.queriesHitBackfaces = true;
            CriarRastreadores();
            CriarAcoesGiro();
        }

        void Start()
        {
            AplicarModo(UnityEngine.XR.XRSettings.isDeviceActive);
            StartCoroutine(VigiarDispositivo());
        }

        void OnDestroy()
        {
            foreach (var a in acoes) a.Dispose();
            acoes.Clear();
        }

        // O OpenXR pode ficar pronto alguns quadros depois do Start (ou cair se o headset desligar).
        IEnumerator VigiarDispositivo()
        {
            var espera = new WaitForSeconds(1f);
            while (true)
            {
                yield return espera;
                bool ativo = UnityEngine.XR.XRSettings.isDeviceActive;
                if (ativo != ModoVR) AplicarModo(ativo);
            }
        }

        void AplicarModo(bool vr)
        {
            ModoVR = vr;
            if (jogadorDesktop != null) jogadorDesktop.SetActive(!vr);
            if (cameraVR != null) cameraVR.gameObject.SetActive(vr);
            if (maoEsquerda != null) maoEsquerda.gameObject.SetActive(vr);
            if (maoDireita != null) maoDireita.gameObject.SetActive(vr);
            if (vr) DefinirOrigemNoChao();
            Debug.Log("[RigVR] Modo " + (vr ? "VR (headset ativo)" : "desktop (sem headset)"));
        }

        void CriarRastreadores()
        {
            if (cameraVR != null)
                Rastrear(cameraVR.transform, "<XRHMD>/centerEyePosition", "<XRHMD>/centerEyeRotation");
            // pointerPosition/pointerRotation = pose de mira (aim) nos perfis OpenXR; aponta como um laser.
            Rastrear(maoEsquerda, "<XRController>{LeftHand}/pointerPosition", "<XRController>{LeftHand}/pointerRotation");
            Rastrear(maoDireita, "<XRController>{RightHand}/pointerPosition", "<XRController>{RightHand}/pointerRotation");
        }

        void Rastrear(Transform alvo, string caminhoPosicao, string caminhoRotacao)
        {
            if (alvo == null) return;
            var tpd = alvo.GetComponent<TrackedPoseDriver>();
            if (tpd == null) tpd = alvo.gameObject.AddComponent<TrackedPoseDriver>();
            var pos = new InputAction(alvo.name + "_Posicao", InputActionType.Value, caminhoPosicao, expectedControlType: "Vector3");
            var rot = new InputAction(alvo.name + "_Rotacao", InputActionType.Value, caminhoRotacao, expectedControlType: "Quaternion");
            acoes.Add(pos);
            acoes.Add(rot);
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.ignoreTrackingState = true;
            tpd.positionInput = new InputActionProperty(pos);
            tpd.rotationInput = new InputActionProperty(rot);
            pos.Enable();
            rot.Enable();
        }

        void CriarAcoesGiro()
        {
            acaoGiroEsq = new InputAction("Giro_Esq", InputActionType.Value, "<XRController>{LeftHand}/thumbstick", expectedControlType: "Vector2");
            acaoGiroDir = new InputAction("Giro_Dir", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
            acoes.Add(acaoGiroEsq);
            acoes.Add(acaoGiroDir);
            acaoGiroEsq.Enable();
            acaoGiroDir.Enable();
        }

        void Update()
        {
            if (!ModoVR) return;
            Vector2 esq = acaoGiroEsq.ReadValue<Vector2>();
            float x = acaoGiroDir.ReadValue<Vector2>().x;
            if (movimentoContinuo) Andar(esq, Time.deltaTime);
            else if (Mathf.Abs(esq.x) > Mathf.Abs(x)) x = esq.x;

            if (!giroEmCurso && Mathf.Abs(x) > zonaMorta)
            {
                Girar(Mathf.Sign(x) * anguloGiro);
                giroEmCurso = true;
            }
            else if (giroEmCurso && Mathf.Abs(x) < 0.3f)
            {
                giroEmCurso = false;
            }
        }

        // Anda no plano do chão, para onde a cabeça aponta. Desliza ao encostar em paredes e móveis.
        void Andar(Vector2 eixo, float dt)
        {
            float m = eixo.magnitude;
            if (m < zonaMortaMovimento) return;
            float intensidade = Mathf.Clamp01((m - zonaMortaMovimento) / (1f - zonaMortaMovimento));
            Transform cab = cameraVR != null ? cameraVR.transform : transform;
            Vector3 frente = Vector3.ProjectOnPlane(cab.forward, Vector3.up);
            if (frente.sqrMagnitude < 0.0001f) frente = Vector3.ProjectOnPlane(cab.up, Vector3.up);
            frente.Normalize();
            Vector3 direita = Vector3.Cross(Vector3.up, frente);
            Vector3 passo = (frente * eixo.y + direita * eixo.x).normalized * (velocidade * intensidade * dt);

            // Esfera na altura da cintura: acerta paredes e móveis, ignora tapetes e os gatilhos dos riscos.
            Vector3 origem = new Vector3(cab.position.x, transform.position.y + 0.6f, cab.position.z);
            for (int i = 0; i < 2 && passo.sqrMagnitude > 1e-8f; i++)
            {
                float dist = passo.magnitude;
                if (!Physics.SphereCast(origem, raioCorpo, passo / dist, out RaycastHit hit, dist + 0.05f, camadasColisao, QueryTriggerInteraction.Ignore))
                    break;
                Vector3 normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
                if (normal.sqrMagnitude < 0.0001f) break;
                float livre = Mathf.Max(0f, hit.distance - 0.05f);
                Vector3 ate = passo / dist * livre;
                passo = ate + Vector3.ProjectOnPlane(passo - ate, normal.normalized);
                if (i == 1 && Physics.SphereCast(origem, raioCorpo, passo.normalized, out _, passo.magnitude + 0.05f, camadasColisao, QueryTriggerInteraction.Ignore))
                    passo = ate;
            }
            transform.position += passo;
        }

        // Roda depois do rastreamento da cabeça. Garante que a cabeça não cruzou nenhuma parede desde o último quadro
        // e que continua dentro da casa; se não, devolve a pessoa para o último ponto válido.
        void LateUpdate()
        {
            if (!ModoVR || cameraVR == null) return;
            Vector3 cab = cameraVR.transform.position;

            if (!tetoVerificado)
            {
                tetoVerificado = true;
                tetoDisponivel = DentroDaCasa(cab);
                if (voltarSeSairDaCasa && !tetoDisponivel)
                    Debug.LogWarning("[RigVR] Não encontrei teto com colisor acima do ponto de início; a volta automática para dentro da casa ficou desligada.");
            }
            if (!temCabecaValida)
            {
                ultimaCabecaValida = cab;
                temCabecaValida = true;
                return;
            }

            if (impedirAtravessarParedes && CruzaParede(ultimaCabecaValida, cab))
            {
                Deslocar(ultimaCabecaValida - cab);
                return;
            }
            if (voltarSeSairDaCasa && tetoDisponivel && !DentroDaCasa(cab))
            {
                tempoFora += Time.deltaTime;
                if (tempoFora >= tempoForaAntesDeVoltar)
                {
                    tempoFora = 0f;
                    Deslocar(ultimaCabecaValida - cab);
                    Debug.Log("[RigVR] Saiu da casa; voltando para o último ponto dentro.");
                }
                return; // fora da casa não vira ponto válido
            }
            tempoFora = 0f;
            ultimaCabecaValida = cab;
        }

        // Só o movimento horizontal conta (agachar ou levantar não é atravessar parede), na altura atual da cabeça.
        bool CruzaParede(Vector3 de, Vector3 para)
        {
            de.y = para.y;
            Vector3 d = para - de;
            float dist = d.magnitude;
            if (dist < 0.0005f) return false;
            Vector3 dir = d / dist;
            const float folga = 0.1f; // a cabeça não encosta no plano da parede
            return Physics.Raycast(de, dir, dist + folga, camadasColisao, QueryTriggerInteraction.Ignore)
                || Physics.Raycast(para + dir * folga, -dir, dist + folga, camadasColisao, QueryTriggerInteraction.Ignore);
        }

        bool DentroDaCasa(Vector3 cabeca)
        {
            return Physics.Raycast(cabeca, Vector3.up, alturaMaximaTeto, camadasColisao, QueryTriggerInteraction.Ignore);
        }

        // Move o rig só no plano do chão (a altura real da pessoa não muda).
        void Deslocar(Vector3 delta)
        {
            delta.y = 0f;
            transform.position += delta;
        }

        // Depois de um deslocamento intencional (teleporte, reinício), o novo ponto vira a referência.
        void RedefinirReferencia()
        {
            temCabecaValida = false;
            tempoFora = 0f;
        }

        public void Girar(float graus)
        {
            Vector3 pivo = cameraVR != null ? cameraVR.transform.position : transform.position;
            transform.RotateAround(pivo, Vector3.up, graus);
        }

        // Leva o participante até o ponto (a câmera fica exatamente sobre o destino, no chão).
        public void Teleportar(Vector3 destinoChao)
        {
            if (!ModoVR)
            {
                if (jogadorDesktop == null) return;
                var cc = jogadorDesktop.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                jogadorDesktop.transform.position = destinoChao;
                if (cc != null) cc.enabled = true;
                return;
            }
            Vector3 posCam = cameraVR != null ? cameraVR.transform.position : transform.position;
            Vector3 delta = new Vector3(posCam.x - transform.position.x, 0f, posCam.z - transform.position.z);
            transform.position = new Vector3(destinoChao.x - delta.x, destinoChao.y, destinoChao.z - delta.z);
            RedefinirReferencia();
        }

        public void ReposicionarNoInicio(Vector3 posicao, Quaternion rotacao)
        {
            transform.SetPositionAndRotation(posicao, rotacao);
            RedefinirReferencia();
            if (ModoVR)
            {
                Teleportar(posicao);
            }
            else if (jogadorDesktop != null)
            {
                var cc = jogadorDesktop.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                jogadorDesktop.transform.SetPositionAndRotation(posicao, rotacao);
                if (cc != null) cc.enabled = true;
            }
        }

        // Origem no chão: a altura real do participante vale no mundo virtual (em pé ou sentado).
        public void DefinirOrigemNoChao()
        {
            var subsistemas = new List<UnityEngine.XR.XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsistemas);
            foreach (var s in subsistemas)
            {
                if (!s.TrySetTrackingOriginMode(UnityEngine.XR.TrackingOriginModeFlags.Floor))
                    Debug.LogWarning("[RigVR] Não foi possível definir a origem de rastreamento no chão.");
            }
        }
    }
}
