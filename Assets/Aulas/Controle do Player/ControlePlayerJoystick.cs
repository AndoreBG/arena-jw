using UnityEngine;
using UnityEngine.InputSystem;

// Versão nova: as ações não ficam mais no Inspector do Player.
// Elas vêm do ControlesDoJogo, que é o mesmo lugar que o Menu configura.
[RequireComponent(typeof(Rigidbody))]
public class ControlePlayerJoystick : MonoBehaviour
{
    [Header("Movimento")]
    public float velocidadeMovimento = 5f;

    [Header("Tiro")]
    public GameObject prefabProjetil;
    public Transform pontoDeDisparo;
    public float velocidadeProjetil = 12f;
    public float tempoDeVidaProjetil = 3f;

    private Rigidbody corpoFisico;
    private Vector2 direcaoDoControle;

    void Awake()
    {
        corpoFisico = GetComponent<Rigidbody>();
    }

    void Update()
    {
        ControlesDoJogo controles = ControlesDoJogo.Instancia;

        // x = esquerda/direita, y = cima/baixo, de -1 a 1
        direcaoDoControle = controles.AcaoMover.ReadValue<Vector2>();

        if (controles.AcaoAtirar.WasPressedThisFrame())
        {
            Atirar();
        }
    }

    void FixedUpdate()
    {
        // O "cima" do stick vira o eixo Z da arena (vista de cima)
        Vector3 direcaoNaArena = new Vector3(direcaoDoControle.x, 0f, direcaoDoControle.y);

        corpoFisico.linearVelocity = new Vector3(
            direcaoNaArena.x * velocidadeMovimento,
            corpoFisico.linearVelocity.y,
            direcaoNaArena.z * velocidadeMovimento);

        bool estaSeMovendo = direcaoNaArena.sqrMagnitude > 0.01f;
        if (estaSeMovendo)
        {
            corpoFisico.MoveRotation(Quaternion.LookRotation(direcaoNaArena));
        }
    }

    private void Atirar()
    {
        GameObject projetil = Instantiate(prefabProjetil, pontoDeDisparo.position, pontoDeDisparo.rotation);

        Rigidbody corpoDoProjetil = projetil.GetComponent<Rigidbody>();
        corpoDoProjetil.linearVelocity = pontoDeDisparo.forward * velocidadeProjetil;

        Destroy(projetil, tempoDeVidaProjetil);
    }
}
