using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Flutuação da nave: mantém a nave a <c>hoverHeight</c> metros acima do NavMesh, com uma
/// variação senoidal de ±<c>amplitude</c> no eixo Y.
///
/// Por que <c>baseOffset</c>: o NavMeshAgent continua sendo a autoridade do movimento. Elevar o
/// agente com <c>baseOffset</c> mantém a nave no ar sem tirá-la do NavMesh — o path following,
/// a chegada ao destino e a esquiva continuam funcionando. A oscilação é só a soma senoidal sobre
/// esse offset.
///
/// Modo alternativo (agente com <c>updatePosition = false</c>): a altura é aplicada direto no
/// Transform. Use este modo só se algum outro script já estiver dirigindo o Transform.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Arena/Enemy/Enemy Hover")]
public sealed class EnemyHover : MonoBehaviour
{
    [Header("Voo")]
    [Tooltip("Altura de cruzeiro acima do NavMesh.")]
    [SerializeField, Min(0f)] private float hoverHeight = 3f;

    [Tooltip("Amplitude da oscilação vertical, em metros (o planejamento pede ±0,5).")]
    [SerializeField, Min(0f)] private float amplitude = 0.5f;

    [Tooltip("Velocidade da oscilação, em ciclos por segundo.")]
    [SerializeField, Min(0.01f)] private float frequency = 0.35f;

    [Tooltip("Suavização da variação de altura. Zero aplica na hora.")]
    [SerializeField, Min(0f)] private float sharpness = 3f;

    private NavMeshAgent agent;
    private float phase;
    private float currentOffset;
    private float baseHeight;
    private bool warned;

    /// <summary>Altura de voo atual (cruzeiro + oscilação).</summary>
    public float CurrentHoverHeight => hoverHeight + Mathf.Sin(phase) * amplitude;

    /// <summary>Altura de cruzeiro configurada.</summary>
    public float HoverHeight => hoverHeight;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        phase = Random.value * Mathf.PI * 2f;
        baseHeight = transform.position.y;
        currentOffset = hoverHeight;
    }

    private void OnEnable()
    {
        if (agent != null && agent.updatePosition) agent.baseOffset = hoverHeight;
    }

    private void LateUpdate()
    {
        phase += Time.deltaTime * frequency * Mathf.PI * 2f;
        if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;

        float target = hoverHeight + Mathf.Sin(phase) * amplitude;
        currentOffset = sharpness <= 0f
            ? target
            : Mathf.Lerp(currentOffset, target, 1f - Mathf.Exp(-sharpness * Time.deltaTime));

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            // Sem agente: oscila em torno da altura em que nasceu (fallback de robustez).
            Vector3 position = transform.position;
            position.y = Mathf.Lerp(baseHeight, baseHeight + currentOffset, 0.25f);
            transform.position = position;
            return;
        }

        if (agent.updatePosition)
        {
            // Caminho normal: o agente move a nave no plano; aqui só ajustamos a altura de voo.
            agent.baseOffset = currentOffset;
            return;
        }

        // Modo alternativo: o Transform é dirigido aqui, a partir da posição simulada do agente.
        if (!warned)
        {
            warned = true;
            Debug.LogWarning("[EnemyHover] NavMeshAgent com updatePosition = false: a altura é aplicada " +
                             "direto no Transform. O modo recomendado é updatePosition = true + baseOffset.", this);
        }

        Vector3 next = agent.nextPosition;
        transform.position = new Vector3(next.x, next.y + currentOffset, next.z);
        agent.nextPosition = new Vector3(next.x, next.y, next.z);
    }
}
