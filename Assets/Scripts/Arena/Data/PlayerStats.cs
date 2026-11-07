using UnityEngine;

/// <summary>Números do jogador. O <c>ArenaManager</c> aplica os valores ao entrar em cada partida.</summary>
[CreateAssetMenu(menuName = "Arena/Player Stats", fileName = "PlayerStats")]
public sealed class PlayerStats : ScriptableObject
{
    [Header("Vida")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Header("Movimento")]
    [Tooltip("Velocidade máxima para frente (m/s).")]
    [SerializeField, Min(1f)] private float moveSpeed = 8f;

    [Tooltip("Velocidade máxima de giro do casco (graus/s).")]
    [SerializeField, Min(1f)] private float turnSpeed = 110f;

    [Header("Arma")]
    [Tooltip("Dano de cada projétil do jogador.")]
    [SerializeField, Min(1f)] private float shotDamage = 50f;

    [Tooltip("Intervalo entre disparos (segundos).")]
    [SerializeField, Min(0.01f)] private float fireRate = 0.2f;

    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float TurnSpeed => turnSpeed;
    public float ShotDamage => shotDamage;
    public float FireRate => fireRate;

    /// <summary>Aplica os números no jogador da cena (idempotente; pode ser chamado a cada partida).</summary>
    public void ApplyTo(Vida life, TankMovement movement, TankPlayerInput input)
    {
        if (life != null) life.Configure(maxHealth, restore: true);
        if (movement != null) movement.Configure(maxForwardSpeed: moveSpeed, maxTurnSpeed: turnSpeed);
        if (input != null) input.Configure(shotDamage, fireRate);
    }
}
