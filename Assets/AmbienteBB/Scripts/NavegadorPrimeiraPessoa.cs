using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Navegação simples em primeira pessoa para testar a cena no editor (sem VR).
// WASD/setas andam, mouse olha, Shift corre. Esc solta o mouse; clique prende de novo.
[RequireComponent(typeof(CharacterController))]
public class NavegadorPrimeiraPessoa : MonoBehaviour
{
    [SerializeField] Transform cameraAlvo;
    [SerializeField] float alturaOlhos = 1.65f;
    [SerializeField] float velocidade = 1.5f;
    [SerializeField] float velocidadeCorrida = 3f;
    [SerializeField] float sensibilidade = 0.1f;

    CharacterController controle;
    float yaw;
    float pitch;
    float velocidadeVertical;

    void Awake()
    {
        controle = GetComponent<CharacterController>();
        controle.height = 1.8f;
        controle.radius = 0.25f;
        controle.center = new Vector3(0f, 0.9f, 0f);
        controle.stepOffset = 0.25f;
        if (cameraAlvo == null && Camera.main != null) cameraAlvo = Camera.main.transform;
        if (cameraAlvo != null)
        {
            cameraAlvo.SetParent(transform, false);
            cameraAlvo.localPosition = new Vector3(0f, alturaOlhos, 0f);
            cameraAlvo.localRotation = Quaternion.identity;
        }
        yaw = transform.eulerAngles.y;
    }

    void OnEnable() => PrenderCursor(true);

    void OnDisable() => PrenderCursor(false);

    void Update()
    {
        LerEntrada(out Vector2 mover, out Vector2 olhar, out bool correr, out bool soltar, out bool clicar);

        if (soltar) PrenderCursor(false);
        if (clicar) PrenderCursor(true);

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += olhar.x * sensibilidade;
            pitch = Mathf.Clamp(pitch - olhar.y * sensibilidade, -80f, 80f);
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraAlvo != null) cameraAlvo.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 direcao = transform.right * mover.x + transform.forward * mover.y;
        if (direcao.sqrMagnitude > 1f) direcao.Normalize();

        velocidadeVertical = controle.isGrounded ? -1f : velocidadeVertical + Physics.gravity.y * Time.deltaTime;
        Vector3 deslocamento = direcao * (correr ? velocidadeCorrida : velocidade) + Vector3.up * velocidadeVertical;
        controle.Move(deslocamento * Time.deltaTime);
    }

    static void PrenderCursor(bool prender)
    {
        Cursor.lockState = prender ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !prender;
    }

    static void LerEntrada(out Vector2 mover, out Vector2 olhar, out bool correr, out bool soltar, out bool clicar)
    {
        mover = Vector2.zero;
        olhar = Vector2.zero;
        correr = soltar = clicar = false;
#if ENABLE_INPUT_SYSTEM
        var teclado = Keyboard.current;
        var mouse = Mouse.current;
        if (teclado != null)
        {
            mover.x = (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed ? 1f : 0f) - (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed ? 1f : 0f);
            mover.y = (teclado.wKey.isPressed || teclado.upArrowKey.isPressed ? 1f : 0f) - (teclado.sKey.isPressed || teclado.downArrowKey.isPressed ? 1f : 0f);
            correr = teclado.leftShiftKey.isPressed;
            soltar = teclado.escapeKey.wasPressedThisFrame;
        }
        if (mouse != null)
        {
            olhar = mouse.delta.ReadValue();
            clicar = mouse.leftButton.wasPressedThisFrame;
        }
#else
        mover = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        olhar = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        correr = Input.GetKey(KeyCode.LeftShift);
        soltar = Input.GetKeyDown(KeyCode.Escape);
        clicar = Input.GetMouseButtonDown(0);
#endif
    }
}
