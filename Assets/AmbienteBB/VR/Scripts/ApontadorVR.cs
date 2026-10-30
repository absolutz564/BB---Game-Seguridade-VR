using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace AmbienteBB.VR
{
    // Raio de apontar de um controle (ou da câmera, no modo desktop de teste).
    // Diz para o JogoRiscos o que está sob a mira e se o gatilho está pressionado.
    // A pose do controle é aplicada por um TrackedPoseDriver (criado pelo RigVR).
    public class ApontadorVR : MonoBehaviour
    {
        public enum Fonte { MaoEsquerda, MaoDireita, Desktop }

        public Fonte fonte = Fonte.MaoDireita;
        public float alcance = 12f;
        public LayerMask camadas = ~0;
        [Range(0.1f, 0.9f)] public float limiarGatilho = 0.55f;

        [Header("Visual")]
        public Material materialLinha;
        public float larguraLinha = 0.006f;
        public Color corLinha = new Color(0.4f, 0.7f, 1f, 0.55f);
        public Color corLinhaDestaque = new Color(1f, 0.85f, 0.2f, 0.9f);
        public float tamanhoReticulo = 0.03f;
        public bool mostrarLinha = true;

        public PontoDeRisco RiscoSobMira { get; private set; }
        public PontoDeTeleporte TeleporteSobMira { get; private set; }
        public bool GatilhoPressionado { get; private set; }
        public bool GatilhoApertadoNesteQuadro { get; private set; }
        public bool TemHit { get; private set; }
        public RaycastHit UltimoHit { get; private set; }
        public bool Ativo => fonte == Fonte.Desktop || UnityEngine.XR.XRSettings.isDeviceActive;

        LineRenderer linha;
        Transform reticulo;
        Renderer reticuloRend;
        MaterialPropertyBlock bloco;
        InputAction acaoGatilho;
        bool gatilhoAnterior;
        static readonly int IdCor = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            ConstruirVisual();
            ConfigurarEntrada();
        }

        void OnEnable()
        {
            if (acaoGatilho != null) acaoGatilho.Enable();
        }

        void OnDisable()
        {
            if (acaoGatilho != null) acaoGatilho.Disable();
            Mirar(null, null);
            GatilhoPressionado = false;
            GatilhoApertadoNesteQuadro = false;
            gatilhoAnterior = false;
            if (linha != null) linha.enabled = false;
            if (reticulo != null) reticulo.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (acaoGatilho != null) acaoGatilho.Dispose();
        }

        void ConfigurarEntrada()
        {
            if (fonte == Fonte.Desktop) return;
            string mao = fonte == Fonte.MaoEsquerda ? "LeftHand" : "RightHand";
            acaoGatilho = new InputAction("Gatilho_" + mao, InputActionType.Value, "<XRController>{" + mao + "}/trigger");
            acaoGatilho.AddBinding("<XRController>{" + mao + "}/triggerPressed");
        }

        void ConstruirVisual()
        {
            var goLinha = new GameObject("Linha");
            goLinha.transform.SetParent(transform, false);
            linha = goLinha.AddComponent<LineRenderer>();
            linha.useWorldSpace = true;
            linha.positionCount = 2;
            linha.startWidth = larguraLinha;
            linha.endWidth = larguraLinha * 0.5f;
            linha.numCapVertices = 4;
            linha.alignment = LineAlignment.View;
            linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            linha.receiveShadows = false;
            linha.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            if (materialLinha != null) linha.sharedMaterial = materialLinha;
            linha.enabled = false;

            var goRet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            goRet.name = "Reticulo";
            var col = goRet.GetComponent<Collider>();
            if (col != null) Destroy(col);
            goRet.transform.SetParent(transform, false);
            reticulo = goRet.transform;
            reticuloRend = goRet.GetComponent<Renderer>();
            if (materialLinha != null) reticuloRend.sharedMaterial = materialLinha;
            reticuloRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            reticuloRend.receiveShadows = false;
            reticuloRend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            goRet.SetActive(false);
        }

        Ray ObterRaio()
        {
            if (fonte == Fonte.Desktop)
            {
                var cam = Camera.main;
                if (cam != null) return new Ray(cam.transform.position, cam.transform.forward);
            }
            return new Ray(transform.position, transform.forward);
        }

        bool LerGatilho()
        {
            if (fonte == Fonte.Desktop)
            {
                var m = Mouse.current;
                var k = Keyboard.current;
                return (m != null && m.leftButton.isPressed) || (k != null && k.spaceKey.isPressed);
            }
            return acaoGatilho != null && acaoGatilho.ReadValue<float>() >= limiarGatilho;
        }

        void Update()
        {
            GatilhoApertadoNesteQuadro = false;
            if (!Ativo)
            {
                Mirar(null, null);
                if (linha != null) linha.enabled = false;
                if (reticulo != null) reticulo.gameObject.SetActive(false);
                return;
            }

            Ray raio = ObterRaio();
            bool hit = Physics.Raycast(raio, out RaycastHit info, alcance, camadas, QueryTriggerInteraction.Collide);
            TemHit = hit;
            UltimoHit = info;
            Vector3 fim = hit ? info.point : raio.origin + raio.direction * alcance;

            PontoDeRisco risco = hit ? info.collider.GetComponentInParent<PontoDeRisco>() : null;
            if (risco != null && !risco.Interativo) risco = null;
            PontoDeTeleporte tele = hit ? info.collider.GetComponentInParent<PontoDeTeleporte>() : null;
            if (tele != null && !tele.Visivel) tele = null;
            Mirar(risco, tele);

            bool pressionado = LerGatilho();
            GatilhoApertadoNesteQuadro = pressionado && !gatilhoAnterior;
            gatilhoAnterior = pressionado;
            GatilhoPressionado = pressionado;

            bool destaque = risco != null || tele != null;
            Color cor = destaque ? corLinhaDestaque : corLinha;
            if (linha != null)
            {
                linha.enabled = mostrarLinha && fonte != Fonte.Desktop;
                linha.SetPosition(0, raio.origin);
                linha.SetPosition(1, fim);
                linha.startColor = cor;
                linha.endColor = new Color(cor.r, cor.g, cor.b, cor.a * 0.15f);
            }
            if (reticulo != null)
            {
                reticulo.gameObject.SetActive(hit);
                reticulo.position = fim;
                reticulo.localScale = Vector3.one * tamanhoReticulo * (destaque ? 1.6f : 1f);
                if (bloco == null) bloco = new MaterialPropertyBlock();
                bloco.SetColor(IdCor, cor);
                reticuloRend.SetPropertyBlock(bloco);
            }
        }

        void Mirar(PontoDeRisco risco, PontoDeTeleporte tele)
        {
            if (RiscoSobMira != risco)
            {
                if (RiscoSobMira != null) RiscoSobMira.Hover(false);
                RiscoSobMira = risco;
                if (RiscoSobMira != null)
                {
                    RiscoSobMira.Hover(true);
                    Vibrar(0.25f, 0.04f);
                }
            }
            if (TeleporteSobMira != tele)
            {
                if (TeleporteSobMira != null) TeleporteSobMira.Hover(false);
                TeleporteSobMira = tele;
                if (TeleporteSobMira != null) TeleporteSobMira.Hover(true);
            }
        }

        // Vibração curta no controle (ignorado no desktop ou se o dispositivo não suportar).
        public void Vibrar(float intensidade, float duracao)
        {
            if (fonte == Fonte.Desktop) return;
            var uso = fonte == Fonte.MaoEsquerda
                ? UnityEngine.InputSystem.CommonUsages.LeftHand
                : UnityEngine.InputSystem.CommonUsages.RightHand;
            var dispositivo = InputSystem.GetDevice<XRControllerWithRumble>(uso);
            if (dispositivo != null) dispositivo.SendImpulse(intensidade, duracao);
        }
    }
}
