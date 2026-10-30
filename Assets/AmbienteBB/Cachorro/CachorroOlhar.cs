using UnityEngine;
using UnityEngine.Animations.Rigging;

// Visual: faz o cachorro olhar para a câmera do jogador usando o Multi-Aim da cabeça (Animation Rigging).
// Quando o jogador sai do campo de visão (vai para trás do cachorro), a cabeça volta suavemente a olhar para a frente,
// em vez de torcer até o limite.
[DisallowMultipleComponent]
public class CachorroOlhar : MonoBehaviour
{
    [Tooltip("Multi-Aim Constraint da cabeça.")]
    public MultiAimConstraint restricao;
    [Tooltip("Alvo que a restrição segue; este componente move ele.")]
    public Transform alvo;
    public Transform cabeca;
    [Tooltip("Vazio = usa a Camera.main (funciona com o XR Origin).")]
    public Transform cameraJogador;

    [Tooltip("Até este ângulo da frente do cachorro, olha para o jogador com peso cheio.")]
    [Range(0, 180)] public float anguloCheio = 70f;
    [Tooltip("A partir deste ângulo, olha só para a frente.")]
    [Range(0, 180)] public float anguloZero = 110f;
    [Tooltip("Mais longe que isso, o cachorro para de acompanhar.")]
    public float distanciaMax = 7f;
    [Range(0, 1)] public float pesoMax = 0.85f;
    [Tooltip("Velocidade com que a cabeça acompanha (maior = mais rápido).")]
    public float suavizacao = 5f;

    Vector3 alvoSuave;
    float peso;

    Vector3 Frente => cabeca.position + transform.forward * 1.5f;

    void OnEnable()
    {
        alvoSuave = Frente;
        if (alvo) alvo.position = alvoSuave;
        peso = 0f;
    }

    void Update()
    {
        if (!restricao || !alvo || !cabeca) return;
        if (!cameraJogador && Camera.main) cameraJogador = Camera.main.transform;

        float fator = 0f;
        Vector3 desejado = Frente;
        if (cameraJogador)
        {
            Vector3 paraCam = cameraJogador.position - cabeca.position;
            Vector3 plano = new Vector3(paraCam.x, 0f, paraCam.z);
            float ang = Vector3.Angle(transform.forward, plano);
            fator = Mathf.InverseLerp(anguloZero, anguloCheio, ang);
            fator *= Mathf.InverseLerp(distanciaMax, distanciaMax * 0.8f, paraCam.magnitude);
            desejado = Vector3.Lerp(Frente, cameraJogador.position, fator);
        }

        float k = 1f - Mathf.Exp(-suavizacao * Time.deltaTime);
        alvoSuave = Vector3.Lerp(alvoSuave, desejado, k);
        peso = Mathf.Lerp(peso, pesoMax * fator, k);
        alvo.position = alvoSuave;
        restricao.weight = peso;
    }
}
