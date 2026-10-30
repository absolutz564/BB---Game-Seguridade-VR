using System.Collections.Generic;
using UnityEngine;

namespace AmbienteBB.VR
{
    public enum EstadoRisco { Bloqueado, Pendente, Protegido }

    // Vai no objeto raiz de cada risco (risco_XXX_NN) e no cachorro.
    // Controla a troca Risco -> Arrumado e expõe a posição onde o indicador flutua.
    [DisallowMultipleComponent]
    public class PontoDeRisco : MonoBehaviour
    {
        public DadosRisco dados;
        [Tooltip("Grupo com a cenografia do risco (filho 'Risco'). Desligado ao proteger.")]
        public GameObject grupoRisco;
        [Tooltip("Grupo com a cenografia consertada (filho 'Arrumado'). Ligado ao proteger.")]
        public GameObject grupoArrumado;
        [Tooltip("Opcional: ponto onde o indicador flutua. Vazio = centro dos renderers/colisores do risco.")]
        public Transform ancoraIndicador;
        public Vector3 deslocamentoIndicador = new Vector3(0f, 0.25f, 0f);

        public EstadoRisco Estado { get; private set; } = EstadoRisco.Pendente;
        public bool Interativo => Estado == EstadoRisco.Pendente && isActiveAndEnabled;
        public bool EPet => dados != null && dados.ePet;
        public IndicadorRisco Indicador { get; set; }

        Bounds limites;
        bool limitesCalculados;

        void Reset() => LocalizarGrupos();

        public void LocalizarGrupos()
        {
            if (grupoRisco == null) grupoRisco = EncontrarFilho("Risco");
            if (grupoArrumado == null) grupoArrumado = EncontrarFilho("Arrumado");
        }

        GameObject EncontrarFilho(string nome)
        {
            foreach (Transform t in transform) if (t.name == nome) return t.gameObject;
            return null;
        }

        public Vector3 PosicaoIndicador
        {
            get
            {
                if (ancoraIndicador != null) return ancoraIndicador.position + deslocamentoIndicador;
                return Limites.center + deslocamentoIndicador;
            }
        }

        // Caixa que envolve a cenografia do risco (ignora o grupo "Arrumado").
        public Bounds Limites
        {
            get
            {
                if (!limitesCalculados) CalcularLimites();
                return limites;
            }
        }

        public void CalcularLimites()
        {
            bool tem = false;
            Bounds b = new Bounds(transform.position, Vector3.zero);
            var renderers = new List<Renderer>();
            GetComponentsInChildren(true, renderers);
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                if (grupoArrumado != null && r.transform.IsChildOf(grupoArrumado.transform)) continue;
                if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds);
            }
            if (!tem)
            {
                var colisores = new List<Collider>();
                GetComponentsInChildren(true, colisores);
                foreach (var c in colisores)
                {
                    if (grupoArrumado != null && c.transform.IsChildOf(grupoArrumado.transform)) continue;
                    if (!tem) { b = c.bounds; tem = true; } else b.Encapsulate(c.bounds);
                }
            }
            limites = b;
            limitesCalculados = true;
        }

        public void DefinirEstado(EstadoRisco novo)
        {
            Estado = novo;
            if (Indicador != null) Indicador.AtualizarEstado(novo);
        }

        public void Hover(bool ativo)
        {
            if (Indicador != null) Indicador.Hover(ativo && Interativo);
        }

        // Troca a cenografia para a versão protegida.
        public void Proteger()
        {
            if (grupoRisco != null) grupoRisco.SetActive(false);
            if (grupoArrumado != null) grupoArrumado.SetActive(true);
            DefinirEstado(EstadoRisco.Protegido);
        }

        // Volta ao estado inicial para o próximo participante.
        public void Reiniciar()
        {
            if (grupoRisco != null) grupoRisco.SetActive(true);
            if (grupoArrumado != null) grupoArrumado.SetActive(false);
            limitesCalculados = false;
            DefinirEstado(EPet ? EstadoRisco.Bloqueado : EstadoRisco.Pendente);
            if (Indicador != null) Indicador.Reposicionar(PosicaoIndicador);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.5f, 1f, 0.6f);
            Gizmos.DrawWireSphere(PosicaoIndicador, 0.12f);
        }
    }
}
